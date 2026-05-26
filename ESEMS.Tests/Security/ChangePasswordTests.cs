using System.Security.Claims;
using ESEMS.Web.Controllers;
using ESEMS.Web.Data;
using ESEMS.Web.Helpers;
using ESEMS.Web.Models;
using ESEMS.Web.Services.Notifications;
using ESEMS.Tests.TestFixtures;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ESEMS.Tests.Security;

/// <summary>
/// F-013: self-service password change on the Profile page. Verifies the
/// AccountController.ChangePassword action enforces the current-password check,
/// the minimum length, and the confirm match — and only rewrites the PBKDF2
/// hash when all checks pass.
/// </summary>
public class ChangePasswordTests
{
    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    private static AccountController NewController(ApplicationDbContext db, int userId)
    {
        var c = new AccountController(db, NullLogger<AccountController>.Instance,
            new MemoryCache(new MemoryCacheOptions()), Mock.Of<INotificationService>(),
            Mock.Of<IConfiguration>());

        // ChangePassword re-issues the cookie via HttpContext.SignInAsync, which
        // resolves IAuthenticationService from RequestServices — provide a no-op.
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IAuthenticationService>());

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "tester"),
            }, "TestAuth")),
            RequestServices = services.BuildServiceProvider()
        };
        c.ControllerContext = new ControllerContext { HttpContext = http };
        c.TempData = new TempDataDictionary(http, new NullTempDataProvider());
        // RedirectToAction reads ControllerBase.Url; with a non-null
        // RequestServices set (above, for SignInAsync) the getter would resolve
        // IUrlHelperFactory from DI — set a stub so it doesn't.
        c.Url = Mock.Of<Microsoft.AspNetCore.Mvc.IUrlHelper>();
        return c;
    }

    private static async Task SeedUserAsync(ApplicationDbContext db, string password)
    {
        db.CustomUsers.Add(new CustomUser { UserId = 1, Username = "tester", IsActive = true, Password = PasswordHelper.Hash(password) });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ChangePassword_WithCorrectCurrent_RehashesToNew()
    {
        using var db = TestDbContextFactory.Create();
        await SeedUserAsync(db, "OldPass123");

        var result = await NewController(db, 1).ChangePassword("OldPass123", "NewPass456", "NewPass456");

        Assert.IsType<RedirectToActionResult>(result);
        var after = await db.CustomUsers.FindAsync(1);
        Assert.True(PasswordHelper.Verify("NewPass456", after!.Password!), "new password should verify");
        Assert.False(PasswordHelper.Verify("OldPass123", after.Password!), "old password should no longer verify");
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrent_LeavesHashUnchanged()
    {
        using var db = TestDbContextFactory.Create();
        await SeedUserAsync(db, "OldPass123");

        await NewController(db, 1).ChangePassword("WRONG-CURRENT", "NewPass456", "NewPass456");

        var after = await db.CustomUsers.FindAsync(1);
        Assert.True(PasswordHelper.Verify("OldPass123", after!.Password!), "password must be unchanged on a wrong current-password");
    }

    [Fact]
    public async Task ChangePassword_MismatchedConfirm_LeavesHashUnchanged()
    {
        using var db = TestDbContextFactory.Create();
        await SeedUserAsync(db, "OldPass123");

        await NewController(db, 1).ChangePassword("OldPass123", "NewPass456", "different-value");

        var after = await db.CustomUsers.FindAsync(1);
        Assert.True(PasswordHelper.Verify("OldPass123", after!.Password!), "password must be unchanged when confirm doesn't match");
    }

    [Fact]
    public async Task ChangePassword_TooShort_LeavesHashUnchanged()
    {
        using var db = TestDbContextFactory.Create();
        await SeedUserAsync(db, "OldPass123");

        await NewController(db, 1).ChangePassword("OldPass123", "short", "short");

        var after = await db.CustomUsers.FindAsync(1);
        Assert.True(PasswordHelper.Verify("OldPass123", after!.Password!), "password must be unchanged when the new password is too short");
    }

    [Fact]
    public async Task ChangePassword_RollsSecurityStamp()
    {
        using var db = TestDbContextFactory.Create();
        await SeedUserAsync(db, "OldPass123");
        var before = (await db.CustomUsers.AsNoTracking().FirstAsync(u => u.UserId == 1)).SecurityStamp; // null at seed

        await NewController(db, 1).ChangePassword("OldPass123", "NewPass456", "NewPass456");

        var after = (await db.CustomUsers.AsNoTracking().FirstAsync(u => u.UserId == 1)).SecurityStamp;
        Assert.False(string.IsNullOrEmpty(after), "FU-002: a fresh security stamp must be set so other sessions are invalidated");
        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task ChangePassword_TooLong_LeavesHashUnchanged()
    {
        using var db = TestDbContextFactory.Create();
        await SeedUserAsync(db, "OldPass123");

        var longPw = new string('a', 200); // exceeds the 128-char cap (FU-003)
        await NewController(db, 1).ChangePassword("OldPass123", longPw, longPw);

        var after = await db.CustomUsers.FindAsync(1);
        Assert.True(PasswordHelper.Verify("OldPass123", after!.Password!), "an over-long (200-char) password must be rejected");
    }

    [Fact]
    public async Task ChangePassword_LocksOutAfterRepeatedWrongCurrent()
    {
        using var db = TestDbContextFactory.Create();
        await SeedUserAsync(db, "OldPass123");

        // Reuse ONE controller so its IMemoryCache (the lockout counter) persists
        // across attempts. Five wrong current-password guesses trip the lockout.
        var c = NewController(db, 1);
        for (var i = 0; i < 5; i++)
            await c.ChangePassword("WRONG-CURRENT", "NewPass456", "NewPass456");

        // 6th attempt — even with the CORRECT current password — is locked out.
        await c.ChangePassword("OldPass123", "NewPass456", "NewPass456");

        var after = await db.CustomUsers.FindAsync(1);
        Assert.True(PasswordHelper.Verify("OldPass123", after!.Password!),
            "after 5 wrong attempts the form must lock out, so the correct-password change is refused");
    }
}
