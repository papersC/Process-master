using ESEMS.Web.Data;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Services.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

namespace ESEMS.Tests.Services;

/// <summary>
/// Adversarial tests for <see cref="AuditSaveChangesInterceptor"/> — top risk #2.
///
/// The audit log is the regulator's evidence trail. If it silently drops on
/// no-op updates, double-writes for cascades, or recurses on AuditLog rows,
/// the trail loses meaning. These tests lock five invariants:
///
///   1. Create produces exactly one log row with NewValues populated.
///   2. Update with a real change populates Old + New + ChangedProperties.
///   3. Tracked save without modifications produces NO log row (noise guard).
///   4. AuditLog rows themselves are NEVER audited (recursion guard).
///   5. Anonymous HttpContext (background jobs) still writes a log with null user.
/// </summary>
public class AuditInterceptorTests
{
    private static AuditSaveChangesInterceptor BuildInterceptor(
        string? userIdClaim = "42",
        string? userName = "test.user",
        string? remoteIp = "203.0.113.7",
        string? userAgent = "xunit/1.0")
    {
        var http = new DefaultHttpContext();
        if (userIdClaim != null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userIdClaim),
                new(ClaimTypes.Name, userName ?? string.Empty),
            };
            http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        }
        if (!string.IsNullOrEmpty(remoteIp))
            http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteIp);
        if (!string.IsNullOrEmpty(userAgent))
            http.Request.Headers.UserAgent = userAgent;

        var accessor = new HttpContextAccessor { HttpContext = http };
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        return new AuditSaveChangesInterceptor(accessor, config, NullLogger<AuditSaveChangesInterceptor>.Instance);
    }

    private static ApplicationDbContext BuildContext(AuditSaveChangesInterceptor interceptor)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("audit-" + Guid.NewGuid())
            .AddInterceptors(interceptor)
            .Options;
        var ctx = new ApplicationDbContext(opts);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task Create_Operation_Produces_Single_AuditLog_With_NewValues()
    {
        await using var ctx = BuildContext(BuildInterceptor());

        ctx.Categories.Add(new Category { Code = "1.0", NameEn = "Strategy", NameAr = "إستراتيجية" });
        await ctx.SaveChangesAsync();

        var logs = await ctx.AuditLogs.Where(a => a.EntityType == nameof(Category)).ToListAsync();
        Assert.Single(logs);
        var log = logs[0];
        Assert.Equal(AuditAction.Create, log.Action);
        Assert.Equal("42", log.UserId);
        Assert.False(string.IsNullOrEmpty(log.NewValues));
        Assert.Null(log.OldValues);
        Assert.Equal("203.0.113.7", log.IpAddress);
    }

    [Fact]
    public async Task Update_With_Real_Change_Produces_Log_With_OldAndNewValues()
    {
        await using var ctx = BuildContext(BuildInterceptor());

        var c = new Category { Code = "3.0", NameEn = "Risk" };
        ctx.Categories.Add(c);
        await ctx.SaveChangesAsync();

        c.NameEn = "Risk Management";
        await ctx.SaveChangesAsync();

        var updateLog = await ctx.AuditLogs
            .Where(a => a.EntityType == nameof(Category) && a.Action == AuditAction.Update)
            .SingleAsync();
        Assert.Contains("NameEn", updateLog.ChangedProperties);
        Assert.Contains("Risk", updateLog.OldValues!);
        Assert.Contains("Risk Management", updateLog.NewValues!);
    }

    [Fact]
    public async Task Tracked_Save_Without_Modifications_Produces_No_AuditLog()
    {
        // Reason: tracked entity → SaveChanges with no edits must not produce
        // noise. Detached `.Update(entity)` IS expected to produce a log row
        // (it forces IsModified=true on every property — controllers using
        // detached updates should diff before assigning if they want noise-free
        // logs).
        await using var ctx = BuildContext(BuildInterceptor());

        var c = new Category { Code = "2.0", NameEn = "Operations" };
        ctx.Categories.Add(c);
        await ctx.SaveChangesAsync();

        var auditCountAfterCreate = await ctx.AuditLogs.CountAsync();

        var loaded = await ctx.Categories.SingleAsync(x => x.Code == "2.0");
        Assert.NotNull(loaded);
        await ctx.SaveChangesAsync();

        var auditCountAfter = await ctx.AuditLogs.CountAsync();
        Assert.Equal(auditCountAfterCreate, auditCountAfter);
    }

    [Fact]
    public async Task AuditLog_Itself_Is_Never_Audited_Recursively()
    {
        await using var ctx = BuildContext(BuildInterceptor());

        // Hand-roll an AuditLog row (e.g., login event written by a controller).
        ctx.AuditLogs.Add(new AuditLog
        {
            Action = AuditAction.Login,
            EntityType = "User",
            EntityId = "42",
            Timestamp = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        // The interceptor MUST NOT have generated a meta-audit row whose
        // EntityType is "AuditLog" — that would be infinite-recursion bait.
        var metaAudits = await ctx.AuditLogs.Where(a => a.EntityType == "AuditLog").ToListAsync();
        Assert.Empty(metaAudits);
    }

    [Fact]
    public async Task Anonymous_HttpContext_Still_Writes_Log_With_Null_User()
    {
        // Backend services / hosted workers can save outside an authenticated
        // request. The audit must still record the change with a null user
        // rather than crash or silently skip.
        await using var ctx = BuildContext(BuildInterceptor(userIdClaim: null, remoteIp: null, userAgent: null));

        ctx.Categories.Add(new Category { Code = "4.0", NameEn = "Background-job entity" });
        await ctx.SaveChangesAsync();

        var log = await ctx.AuditLogs.SingleAsync();
        Assert.Null(log.UserId);
        Assert.Equal(AuditAction.Create, log.Action);
    }
}
