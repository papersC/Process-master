using ESEMS.Web.Models.Common;
using ESEMS.Web.Services.Email;
using ESEMS.Tests.TestFixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ESEMS.Tests.Services;

public class SmtpEmailServiceTests
{
    [Fact]
    public void IsEnabled_False_WhenNoAppSettings_AndFallbackDisabled()
    {
        using var db = TestDbContextFactory.Create();
        var svc = Build(db, new EmailOptions { Enabled = false });

        Assert.False(svc.IsEnabled);
    }

    [Fact]
    public void IsEnabled_True_WhenAppSettingsOverrideEnables()
    {
        using var db = TestDbContextFactory.Create();
        db.AppSettings.AddRange(
            new AppSetting { Key = "Smtp.Enabled", Value = "true" },
            new AppSetting { Key = "Smtp.Host", Value = "smtp.mail.local" }
        );
        db.SaveChanges();
        var svc = Build(db, new EmailOptions { Enabled = false }); // fallback disabled

        Assert.True(svc.IsEnabled);
    }

    [Fact]
    public void IsEnabled_False_WhenEnabledButNoHost()
    {
        using var db = TestDbContextFactory.Create();
        db.AppSettings.Add(new AppSetting { Key = "Smtp.Enabled", Value = "true" });
        db.SaveChanges();
        var svc = Build(db, new EmailOptions { Enabled = false });

        Assert.False(svc.IsEnabled);
    }

    [Fact]
    public async Task SendAsync_ReturnsFalse_WhenDisabled_WithoutAttemptingSmtp()
    {
        using var db = TestDbContextFactory.Create();
        var svc = Build(db, new EmailOptions { Enabled = false });

        var ok = await svc.SendAsync("a@b.c", "hi", "<p>hi</p>");

        Assert.False(ok);
    }

    [Fact]
    public async Task SendAsync_ReturnsFalse_WhenToAddressIsEmpty_EvenIfEnabled()
    {
        using var db = TestDbContextFactory.Create();
        db.AppSettings.AddRange(
            new AppSetting { Key = "Smtp.Enabled", Value = "true" },
            new AppSetting { Key = "Smtp.Host",    Value = "smtp.mail.local" }
        );
        db.SaveChanges();
        var svc = Build(db, new EmailOptions { Enabled = false });

        var ok = await svc.SendAsync("", "hi", "<p>hi</p>");

        Assert.False(ok);
    }

    [Fact]
    public async Task TestConnectionAsync_Disabled_ReturnsFriendlyMessage_NotSmtpError()
    {
        using var db = TestDbContextFactory.Create();
        var svc = Build(db, new EmailOptions { Enabled = false });

        var (ok, message) = await svc.TestConnectionAsync();

        Assert.False(ok);
        Assert.Contains("disabled", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestConnectionAsync_Enabled_NoHost_ReportsMissingHost()
    {
        using var db = TestDbContextFactory.Create();
        db.AppSettings.Add(new AppSetting { Key = "Smtp.Enabled", Value = "true" });
        db.SaveChanges();
        var svc = Build(db, new EmailOptions { Enabled = false });

        var (ok, message) = await svc.TestConnectionAsync();

        Assert.False(ok);
        Assert.Contains("Host", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestConnectionAsync_EnabledWithHost_ButNoFromAddress_ReportsMissingFrom()
    {
        using var db = TestDbContextFactory.Create();
        db.AppSettings.AddRange(
            new AppSetting { Key = "Smtp.Enabled", Value = "true" },
            new AppSetting { Key = "Smtp.Host",    Value = "smtp.mail.local" }
        );
        db.SaveChanges();
        // Fallback has a FromAddress default, so blank it out
        var svc = Build(db, new EmailOptions { Enabled = false, FromAddress = "" });

        var (ok, message) = await svc.TestConnectionAsync();

        Assert.False(ok);
        Assert.Contains("From", message, StringComparison.OrdinalIgnoreCase);
    }

    private static SmtpEmailService Build(ESEMS.Web.Data.ApplicationDbContext db, EmailOptions fallback)
    {
        return new SmtpEmailService(db, Options.Create(fallback), NullLogger<SmtpEmailService>.Instance);
    }
}
