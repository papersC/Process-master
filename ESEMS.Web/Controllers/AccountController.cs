using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Helpers;
using ESEMS.Web.Models;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Security;
using ESEMS.Web.Services.Notifications;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for user authentication and account management.
/// Uses cookie authentication against the custom [user] table.
///
/// Per-method [AllowAnonymous] is used here instead of class-level so the
/// global FallbackPolicy (RequireAuthenticatedUser) protects any new action
/// added to this controller. Class-level [AllowAnonymous] silently overrides
/// [Authorize] on individual actions (ASP0026) and bypasses the fallback.
/// </summary>
public class AccountController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AccountController> _logger;
    private readonly IMemoryCache _cache;
    private readonly INotificationService _notifications;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Rate-limit constants. Attempts are tracked in IMemoryCache per
    /// username key, so they survive controller re-creation (scoped DI)
    /// but NOT app pool recycling or multi-instance deployments. For a
    /// distributed deployment, swap IMemoryCache for IDistributedCache.
    /// </summary>
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const string RateLimitPrefix = "LoginAttempt:";
    // F-013 hardening: throttle wrong "current password" guesses on the
    // self-service change form so a hijacked session can't brute-force it.
    private const string PwChangePrefix = "PwChangeAttempt:";

    public AccountController(
        ApplicationDbContext db,
        ILogger<AccountController> logger,
        IMemoryCache cache,
        INotificationService notifications,
        IConfiguration configuration)
    {
        _db = db;
        _logger = logger;
        _cache = cache;
        _notifications = notifications;
        _configuration = configuration;
    }

    /// <summary>
    /// Login page
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (TempData["Error"] is string error)
            ViewData["ErrorMessage"] = error;
        return View();
    }

    /// <summary>
    /// Login action – authenticates against the custom [user] table
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (ModelState.IsValid)
        {
            var key = RateLimitPrefix + model.Username.ToLowerInvariant();

            // Check brute-force lockout via IMemoryCache
            var entry = _cache.Get<(int count, DateTime lastAttempt)?>(key);
            if (entry.HasValue
                && entry.Value.count >= MaxFailedAttempts
                && DateTime.UtcNow - entry.Value.lastAttempt < LockoutDuration)
            {
                _logger.LogWarning("Account locked out for user {Username} due to too many failed attempts.", model.Username);
                ModelState.AddModelError(string.Empty, Localizer["Error_AccountLockedOut"]);
                return View(model);
            }

            // Query custom user table by username or email
            var user = await _db.CustomUsers
                .Include(u => u.OrganizationUnit)
                .FirstOrDefaultAsync(u => u.Username == model.Username || u.EmailAddress == model.Username);

            // FUNC-006: deactivated users (IsActive=false) cannot sign in. Folded
            // into the same branch as a bad password so account state isn't leaked.
            if (user != null && user.IsActive && PasswordHelper.Verify(model.Password, user.Password))
            {
                // Reset failed attempts on successful login
                _cache.Remove(key);

                var principal = await BuildSignInPrincipalAsync(user);

                await HttpContext.SignInAsync("Cookies", principal, new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe
                });

                // Audit log
                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = user.UserId.ToString(),
                    UserName = user.Username,
                    Action = AuditAction.Login,
                    EntityType = nameof(CustomUser),
                    EntityId = user.UserId.ToString(),
                    EntityName = user.Username,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = HttpContext.Request.Headers.UserAgent.ToString(),
                    Notes = "Successful login",
                    Timestamp = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                _logger.LogInformation("User {Username} logged in.", user.Username);
                // Pass the freshly built principal: User isn't updated until the
                // next request, so claim checks in RedirectToLocal must read this.
                return RedirectToLocal(returnUrl, principal);
            }

            // Record failed attempt via IMemoryCache. The entry auto-expires
            // after LockoutDuration, replacing the manual check-and-reset.
            var prev = _cache.Get<(int count, DateTime lastAttempt)?>(key);
            int nextCount = (prev.HasValue && DateTime.UtcNow - prev.Value.lastAttempt < LockoutDuration)
                ? prev.Value.count + 1
                : 1;
            _cache.Set(key, ((int, DateTime)?)(nextCount, DateTime.UtcNow),
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = LockoutDuration });

            ModelState.AddModelError(string.Empty, Localizer["Error_InvalidCredentials"]);
        }

        return View(model);
    }

    /// <summary>
    /// Windows Authentication login (for MBRHE IIS deployment)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> WindowsLogin(string? returnUrl = null)
    {
        if (!TryGetWindowsAuthenticatedAccount(out var windowsName, out var username))
        {
            if (!OperatingSystem.IsWindows())
            {
                TempData["Error"] = Localizer["Error_WindowsAuthNotConfigured"].Value;
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            return Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = Url.Action(nameof(WindowsLogin), new { returnUrl })
                },
                NegotiateDefaults.AuthenticationScheme);
        }

        var user = await FindActiveUserByLoginNameAsync(username);

        if (user == null)
        {
            var inactive = await _db.CustomUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => !u.IsActive && u.Username.ToLower() == username.ToLower());
            if (inactive != null)
            {
                _logger.LogWarning("Windows user {WindowsName} maps to inactive account {Username}.", windowsName, username);
                TempData["Error"] = Localizer["Error_WindowsUserInactive"].Value;
                return RedirectToAction(nameof(Login), new { returnUrl });
            }
        }

        user ??= await TryProvisionWindowsUserAsync(username, windowsName);

        if (user != null)
        {
            var winPrincipal = await BuildSignInPrincipalAsync(user, windowsAuth: true);
            await HttpContext.SignInAsync("Cookies", winPrincipal);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = user.UserId.ToString(),
                UserName = user.Username,
                Action = AuditAction.Login,
                EntityType = nameof(CustomUser),
                EntityId = user.UserId.ToString(),
                EntityName = user.Username,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = HttpContext.Request.Headers.UserAgent.ToString(),
                Notes = "Windows authentication login",
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            _logger.LogInformation("User {Username} logged in via Windows Authentication ({WindowsName}).", user.Username, windowsName);
            return RedirectToLocal(returnUrl, winPrincipal);
        }

        var autoCreate = _configuration.GetValue("Authentication:AutoCreateUsers", false);
        _logger.LogWarning(
            "Windows login failed for {WindowsName} (login name {LoginName}). AutoCreateUsers={AutoCreate}.",
            windowsName,
            username,
            autoCreate);

        TempData["Error"] = string.Format(
            CultureInfo.CurrentUICulture,
            Localizer["Error_WindowsLoginFailedDetail"].Value,
            windowsName,
            username);
        return RedirectToAction(nameof(Login), new { returnUrl });
    }

    /// <summary>
    /// SEC-006: accept only IIS/Negotiate identities (WindowsIdentity or Negotiate ClaimsIdentity),
    /// never a prior cookie session.
    /// </summary>
    private bool TryGetWindowsAuthenticatedAccount(out string windowsName, out string loginName)
    {
        windowsName = string.Empty;
        loginName = string.Empty;

        var identity = HttpContext.User.Identity;
        if (identity is not { IsAuthenticated: true } || string.IsNullOrWhiteSpace(identity.Name))
            return false;

        // Reject cookie-authenticated users clicking "Sign in with Windows".
        if (string.Equals(identity.AuthenticationType, "Cookies", StringComparison.OrdinalIgnoreCase))
            return false;

        var isWindows =
            identity is System.Security.Principal.WindowsIdentity
            || string.Equals(identity.AuthenticationType, NegotiateDefaults.AuthenticationScheme, StringComparison.OrdinalIgnoreCase)
            || string.Equals(identity.AuthenticationType, "Negotiate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(identity.AuthenticationType, "NTLM", StringComparison.OrdinalIgnoreCase);

        if (!isWindows)
            return false;

        windowsName = identity.Name.Trim();
        loginName = ResolveLoginNameFromWindows(windowsName);
        return !string.IsNullOrWhiteSpace(loginName);
    }

    /// <summary>Maps DOMAIN\user or user@domain to the [user].username value.</summary>
    private static string ResolveLoginNameFromWindows(string windowsName)
    {
        if (string.IsNullOrWhiteSpace(windowsName))
            return string.Empty;

        if (windowsName.Contains('@', StringComparison.Ordinal))
            return windowsName.Split('@')[0].Trim();

        if (windowsName.Contains('\\', StringComparison.Ordinal))
            return windowsName.Split('\\')[^1].Trim();

        return windowsName.Trim();
    }

    private async Task<CustomUser?> FindActiveUserByLoginNameAsync(string loginName)
    {
        var key = loginName.ToLowerInvariant();
        return await _db.CustomUsers
            .Include(u => u.OrganizationUnit)
            .FirstOrDefaultAsync(u => u.IsActive && u.Username.ToLower() == key);
    }

    /// <summary>
    /// Logout action
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = User.Identity?.Name;

        await HttpContext.SignOutAsync("Cookies");

        if (!string.IsNullOrWhiteSpace(userId) || !string.IsNullOrWhiteSpace(userName))
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                UserName = userName,
                Action = AuditAction.Logout,
                EntityType = "Account",
                EntityId = userId ?? string.Empty,
                EntityName = userName,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = HttpContext.Request.Headers.UserAgent.ToString(),
                Notes = "Logout",
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        _logger.LogInformation("User logged out.");
        return RedirectToAction("Login");
    }

    /// <summary>
    /// Access denied page
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    /// <summary>
    /// User profile page. Attribute-routed so the default {id?} segment is
    /// rejected — /Account/Profile/123 previously returned the SAME page as
    /// /Account/Profile (id was bound but ignored), which looked like an
    /// IDOR-shaped URL even though no cross-user data was exposed. White-box
    /// QA pass flagged the route smell; this closes it by 404'ing the extra
    /// segment so the URL clearly identifies one and only one resource.
    /// </summary>
    [Authorize]
    [Route("Account/Profile")]
    public async Task<IActionResult> Profile()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
            return RedirectToAction("Login");

        var user = await _db.CustomUsers
            .Include(u => u.OrganizationUnit)
            .Include(u => u.Manager)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
            return NotFound();

        return View(user);
    }

    /// <summary>
    /// F-013: self-service password change. Verifies the caller's current
    /// password, enforces a minimum length, re-hashes with PBKDF2
    /// (<see cref="PasswordHelper"/>), and writes an audit row. The auth cookie
    /// isn't derived from the password, so the user stays signed in afterward.
    /// Attribute-routed to match the Profile page that hosts the form.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [Route("Account/ChangePassword")]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        var ar = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
            return RedirectToAction("Login");

        var user = await _db.CustomUsers.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
            return NotFound();

        // F-013 hardening: lock the change form after repeated wrong current-
        // password guesses (mirrors the Login brute-force lockout) so a hijacked
        // session can't brute-force the current password.
        var rlKey = PwChangePrefix + userId;
        var attempt = _cache.Get<(int count, DateTime last)?>(rlKey);
        if (attempt.HasValue && attempt.Value.count >= MaxFailedAttempts
            && DateTime.UtcNow - attempt.Value.last < LockoutDuration)
        {
            TempData["Error"] = ar
                ? "محاولات كثيرة لتغيير كلمة المرور. حاول مرة أخرى لاحقاً."
                : "Too many password-change attempts. Please try again later.";
            return RedirectToAction(nameof(Profile));
        }

        // Validate. Current-password check first so a wrong session can't probe
        // the strength rules; messages are bilingual (resx has no keys for these).
        string? error = null;
        if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            error = ar ? "يرجى تعبئة جميع الحقول." : "Please fill in all fields.";
        else if (string.IsNullOrEmpty(user.Password) || !PasswordHelper.Verify(currentPassword, user.Password))
        {
            // Record the wrong-current-password attempt toward the lockout window.
            var next = (attempt.HasValue && DateTime.UtcNow - attempt.Value.last < LockoutDuration)
                ? attempt.Value.count + 1 : 1;
            _cache.Set(rlKey, ((int, DateTime)?)(next, DateTime.UtcNow),
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = LockoutDuration });
            error = ar ? "كلمة المرور الحالية غير صحيحة." : "Current password is incorrect.";
        }
        else if (newPassword.Length < 8)
            error = ar ? "يجب أن تتكوّن كلمة المرور الجديدة من 8 أحرف على الأقل." : "The new password must be at least 8 characters.";
        else if (newPassword.Length > 128)
            // FU-003: cap length — PBKDF2 HMAC cost scales with input length, so
            // a multi-megabyte "password" would be a CPU DoS vector.
            error = ar ? "كلمة المرور طويلة جداً (الحد الأقصى 128 حرفاً)." : "The new password is too long (maximum 128 characters).";
        else if (newPassword != confirmPassword)
            error = ar ? "كلمة المرور الجديدة وتأكيدها غير متطابقين." : "New password and confirmation do not match.";
        else if (newPassword == currentPassword)
            error = ar ? "يجب أن تختلف كلمة المرور الجديدة عن الحالية." : "The new password must be different from the current one.";

        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Profile));
        }

        // Success — clear the throttle, re-hash, and roll the security stamp so
        // every OTHER session (carrying the old stamp) is invalidated (FU-002).
        _cache.Remove(rlKey);
        user.Password = PasswordHelper.Hash(newPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = user.UserId.ToString(),
            UserName = user.Username,
            Action = AuditAction.Update,
            EntityType = nameof(CustomUser),
            EntityId = user.UserId.ToString(),
            EntityName = user.Username,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = HttpContext.Request.Headers.UserAgent.ToString(),
            Notes = "Password changed (self-service)",
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Keep THIS session signed in: re-issue the cookie with the new stamp so
        // the user isn't logged out by the validator. All OTHER sessions still
        // carry the old stamp and are rejected on their next validation. Every
        // non-stamp claim is preserved.
        var refreshed = User.Claims.Where(c => c.Type != "SecurityStamp").ToList();
        refreshed.Add(new Claim("SecurityStamp", user.SecurityStamp));
        await HttpContext.SignInAsync("Cookies", new ClaimsPrincipal(new ClaimsIdentity(refreshed, "Cookies")));

        // FU-005: alert the account owner. Best-effort — a notification failure
        // must never roll back or block a completed password change.
        try
        {
            await _notifications.SendAsync(user.UserId,
                "Password changed", "تم تغيير كلمة المرور",
                "Your account password was just changed. If this wasn't you, contact your system administrator immediately.",
                "تم تغيير كلمة مرور حسابك للتو. إذا لم تكن أنت من قام بذلك، فتواصل مع مسؤول النظام فوراً.",
                type: "Security",
                dedupKey: $"pwchange:{user.UserId}:{DateTime.UtcNow:yyyyMMddHHmm}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Password-change notification failed for user {UserId}", user.UserId);
        }

        _logger.LogInformation("User {Username} changed their password (self-service).", user.Username);
        TempData["Success"] = ar ? "تم تغيير كلمة المرور بنجاح." : "Your password has been changed.";
        return RedirectToAction(nameof(Profile));
    }

    /// <summary>
    /// Post-login redirect. Honors a safe local <paramref name="returnUrl"/>
    /// first. Otherwise picks a landing page the user can actually open:
    ///
    /// DYN-002: the Dashboard requires the <c>Reports.View</c> permission, so
    /// sending every user there meant viewers without it bounced straight to
    /// AccessDenied right after a successful login. Now only users who hold
    /// <c>Reports.View</c> (or the <c>*.*</c> admin claim) land on the
    /// Dashboard; everyone else lands on /MySpace, which any authenticated
    /// user can see.
    ///
    /// <paramref name="principal"/> lets the Login POST pass the freshly built
    /// principal — immediately after <c>SignInAsync</c> the controller's
    /// <c>User</c> still reflects the (anonymous) request cookie, so reading
    /// claims off it would always miss. Other callers pass null and fall back
    /// to <c>User</c>.
    /// </summary>
    /// <summary>
    /// When <c>Authentication:AutoCreateUsers</c> is true, inserts a new [user] row
    /// for a first-time Windows logon and assigns the default Plan X RoleGroup
    /// identified by its <c>Code</c> (config key <c>Authentication:DefaultUserRoleGroupCode</c>;
    /// defaults to <c>viewer</c>, matching the seeded read-only group).
    /// </summary>
    private async Task<CustomUser?> TryProvisionWindowsUserAsync(string loginName, string windowsName)
    {
        if (!_configuration.GetValue("Authentication:AutoCreateUsers", false))
            return null;

        var existing = await FindActiveUserByLoginNameAsync(loginName);
        if (existing != null)
            return existing;

        // Default to the seeded 'viewer' RoleGroup. The setting key is
        // `Authentication:DefaultUserRoleGroupCode` and holds a RoleGroup.Code
        // value (e.g. "viewer", "editor"); defaults to "viewer" when unset.
        var defaultGroupCode = _configuration["Authentication:DefaultUserRoleGroupCode"] ?? "viewer";
        var defaultGroup = await _db.RoleGroups.FirstOrDefaultAsync(rg => rg.Code == defaultGroupCode && rg.IsActive);
        if (defaultGroup == null)
        {
            _logger.LogError(
                "AutoCreateUsers: default RoleGroup with Code '{Code}' not found — cannot auto-provision Windows user {LoginName}.",
                defaultGroupCode, loginName);
            return null;
        }

        try
        {
            var user = new CustomUser
            {
                Username = loginName,
                EmployeeName = loginName,
                FullName = windowsName,
                Password = PasswordHelper.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
                SecurityStamp = Guid.NewGuid().ToString("N"),
                IsActive = true,
            };
            _db.CustomUsers.Add(user);
            await _db.SaveChangesAsync();

            _db.UserRoleGroups.Add(new Models.Common.UserRoleGroup
            {
                UserId = user.UserId,
                RoleGroupId = defaultGroup.Id,
                AssignedBy = user.UserId,
                AssignedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync();

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = user.UserId.ToString(),
                UserName = user.Username,
                Action = AuditAction.Create,
                EntityType = nameof(CustomUser),
                EntityId = user.UserId.ToString(),
                EntityName = user.Username,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = HttpContext.Request.Headers.UserAgent.ToString(),
                Notes = $"Auto-provisioned from Windows ({windowsName}), role group {defaultGroup.NameEn}",
                Timestamp = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Auto-provisioned Windows user {LoginName} ({WindowsName}) with role group {RoleGroupName}.",
                loginName,
                windowsName,
                defaultGroup.NameEn);

            return await FindActiveUserByLoginNameAsync(loginName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-provision Windows user {LoginName} ({WindowsName}).", loginName, windowsName);
            return null;
        }
    }

    /// <summary>Builds the cookie principal (permissions, scope) for password or Windows login.</summary>
    private async Task<ClaimsPrincipal> BuildSignInPrincipalAsync(CustomUser user, bool windowsAuth = false)
    {
        var roleGroupPermissions = await _db.UserRoleGroups
            .Where(urg => urg.UserId == user.UserId)
            .Join(_db.RoleGroups.Where(rg => rg.IsActive),
                urg => urg.RoleGroupId,
                rg => rg.Id,
                (urg, rg) => rg.Permissions)
            .ToListAsync();

        var permissionSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var csv in roleGroupPermissions)
        {
            if (string.IsNullOrWhiteSpace(csv)) continue;
            foreach (var p in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                permissionSet.Add(p);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.EmailAddress ?? string.Empty),
            new("FullName", user.EmployeeName ?? user.FullName ?? user.Username),
            new("FullNameAr", user.EmployeeNameAr ?? user.EmployeeName ?? user.Username),
            new("SecurityStamp", user.SecurityStamp ?? string.Empty),
        };
        if (windowsAuth)
            claims.Add(new Claim("WindowsAuth", "true"));

        foreach (var permission in permissionSet)
            claims.Add(new Claim("Permission", permission));

        var roleGroupScopes = await _db.UserRoleGroups
            .Where(urg => urg.UserId == user.UserId)
            .Join(_db.RoleGroups.Where(rg => rg.IsActive),
                urg => urg.RoleGroupId, rg => rg.Id,
                (urg, rg) => rg.ScopeLevel)
            .ToListAsync();

        var effectiveScope = "All";
        if (roleGroupScopes.Count > 0)
        {
            if (roleGroupScopes.Contains("All"))
                effectiveScope = "All";
            else if (roleGroupScopes.Contains("OwningUnit"))
                effectiveScope = "OwningUnit";
            else
                effectiveScope = "Process";
        }
        claims.Add(new Claim("ScopeLevel", effectiveScope));

        if (effectiveScope != "All")
        {
            int? unitId = null;
            if (user.UnitId.HasValue)
            {
                var exists = await _db.OrganizationUnits
                    .AnyAsync(ou => ou.Id == user.UnitId.Value && !ou.IsDeleted && ou.IsActive);
                if (exists) unitId = user.UnitId.Value;
            }

            if (unitId.HasValue)
                claims.Add(new Claim("OrganizationUnitId", unitId.Value.ToString()));
            else
            {
                _logger.LogWarning(
                    "User {Username} has scope {ScopeLevel} but no resolvable org unit (UnitId={UnitId}); applying fail-closed (no data visible) until the unit is mapped.",
                    user.Username, effectiveScope, user.UnitId);
            }
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
    }

    private IActionResult RedirectToLocal(string? returnUrl, ClaimsPrincipal? principal = null)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        var p = principal ?? User;
        var canSeeDashboard = p.HasClaim("Permission", "Reports.View")
                              || p.HasClaim("Permission", "*.*");

        return canSeeDashboard
            ? RedirectToAction("Index", "Dashboard")
            : RedirectToAction("Index", "MySpace");
    }
}

/// <summary>
/// Login view model
/// </summary>
public class LoginViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}
