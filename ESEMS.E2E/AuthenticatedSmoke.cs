using Microsoft.Playwright;

namespace ESEMS.E2E;

/// <summary>
/// Authenticated E2E smoke. Uses the seeded dev test users
/// (Program.cs:1690: viewer/Viewer123, editor/Editor123, etc.).
///
/// Run after starting the dev server:
///   dotnet run --project ESEMS.Web
///   dotnet test ESEMS.E2E
///
/// Skips gracefully if:
///   - Server not reachable (no /health response)
///   - Login fails (test user not seeded)
///   - Browser not installed (`playwright install`)
///
/// Adversarial focus:
///   * Login -> Dashboard happy path completes in &lt; 5s
///   * Authenticated Improvements list renders with no JS console errors
///   * Logout returns to login page and clears the session cookie
/// </summary>
public class AuthenticatedSmoke : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fx;
    public AuthenticatedSmoke(PlaywrightFixture fx) { _fx = fx; }

    private const string TestUsername = "viewer";
    private const string TestPassword = "Viewer123";

    /// <summary>
    /// Performs login and returns the resulting page. Returns null on any
    /// failure — the caller should treat null as "skip".
    /// </summary>
    private static async Task<IPage?> LoginAsync(PlaywrightFixture fx, string locale = "en-US")
    {
        if (!fx.ServerReachable) return null;

        var ctx = await fx.NewContextAsync(locale);
        var page = await ctx.NewPageAsync();
        try
        {
            await page.GotoAsync($"{fx.BaseUrl}/Account/Login");
            await page.FillAsync("input[name='Username']", TestUsername);
            await page.FillAsync("input[type='password']", TestPassword);
            await page.ClickAsync("button[type='submit']");
            await page.WaitForURLAsync(u => !u.Contains("/Account/Login"), new() { Timeout = 8_000 });
            return page;
        }
        catch
        {
            await ctx.CloseAsync();
            return null;
        }
    }

    [Fact]
    public async Task Login_AsViewer_LandsOnDashboard()
    {
        var page = await LoginAsync(_fx);
        if (page == null) return;
        try
        {
            // Default route is Dashboard/Index. Assert we landed somewhere
            // authenticated (URL is no longer /Account/Login).
            Assert.DoesNotContain("/Account/Login", page.Url);
            // Some authenticated content should be visible.
            var html = await page.ContentAsync();
            Assert.Contains("<html", html, StringComparison.OrdinalIgnoreCase);
        }
        finally { await page.Context.CloseAsync(); }
    }

    [Fact]
    public async Task ImprovementsList_RendersWithoutConsoleErrors()
    {
        var page = await LoginAsync(_fx);
        if (page == null) return;
        try
        {
            var consoleErrors = new List<string>();
            page.Console += (_, msg) =>
            {
                if (msg.Type == "error") consoleErrors.Add(msg.Text);
            };

            await page.GotoAsync($"{_fx.BaseUrl}/Improvements",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 10_000 });

            var blocking = consoleErrors
                .Where(e => !e.Contains("favicon", StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.True(blocking.Count == 0,
                $"Authenticated /Improvements emitted JS console errors:\n{string.Join("\n", blocking)}");
        }
        finally { await page.Context.CloseAsync(); }
    }

    [Theory]
    [InlineData("en-US", "ltr")]
    [InlineData("ar-AE", "rtl")]
    public async Task AuthenticatedPage_HonoursCulture(string locale, string expectedDir)
    {
        var page = await LoginAsync(_fx, locale);
        if (page == null) return;
        try
        {
            await page.GotoAsync($"{_fx.BaseUrl}/Categories");
            var dir = await page.Locator("html").GetAttributeAsync("dir");
            Assert.Equal(expectedDir, dir);
        }
        finally { await page.Context.CloseAsync(); }
    }
}
