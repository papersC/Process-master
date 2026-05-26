using Microsoft.Playwright;

namespace ESEMS.E2E;

/// <summary>
/// Culture + login surface smoke. Skips if dev server isn't up or browsers
/// aren't installed.
///
/// Run:
///   dotnet run --project ESEMS.Web
///   dotnet test ESEMS.E2E
///
/// Adversarial focus:
///   * Locale → dir="rtl"/"ltr" wired correctly.
///   * Login form has antiforgery token rendered.
///   * Username + password fields visible in both cultures.
///   * Protected-route GET redirects to login.
/// </summary>
public class LoginAndCultureSmoke : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fx;
    public LoginAndCultureSmoke(PlaywrightFixture fx) { _fx = fx; }

    [Theory]
    [InlineData("en-US", "ltr")]
    [InlineData("ar-AE", "rtl")]
    public async Task LoginPage_Renders_With_Correct_Direction(string locale, string expectedDir)
    {
        if (!_fx.ServerReachable) return;

        await using var ctx = await _fx.NewContextAsync(locale);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/Account/Login");

        var dir = await page.Locator("html").GetAttributeAsync("dir");
        Assert.Equal(expectedDir, dir);

        var token = page.Locator("input[name='__RequestVerificationToken']");
        Assert.True(await token.CountAsync() > 0,
            "Login form is missing the antiforgery token — CSRF gate is open.");
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ar-AE")]
    public async Task LoginPage_Has_Visible_Username_And_Password_Fields(string locale)
    {
        if (!_fx.ServerReachable) return;

        await using var ctx = await _fx.NewContextAsync(locale);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/Account/Login");

        var username = page.Locator("input[name='Username'], input[name='UserName'], input[type='text']").First;
        var password = page.Locator("input[type='password']").First;

        await Assertions.Expect(username).ToBeVisibleAsync(new() { Timeout = 5_000 });
        await Assertions.Expect(password).ToBeVisibleAsync(new() { Timeout = 5_000 });
    }

    [Fact]
    public async Task ProtectedRoute_Without_Auth_Redirects_To_Login()
    {
        if (!_fx.ServerReachable) return;

        await using var ctx = await _fx.NewContextAsync("en-US");
        var page = await ctx.NewPageAsync();
        await page.GotoAsync($"{_fx.BaseUrl}/Categories",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        Assert.Contains("/Account/Login", page.Url, StringComparison.OrdinalIgnoreCase);
    }
}
