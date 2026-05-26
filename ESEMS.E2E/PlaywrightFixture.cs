using Microsoft.Playwright;

namespace ESEMS.E2E;

/// <summary>
/// One Playwright + Browser instance per test class. Tests get a fresh
/// IBrowserContext per culture so en-US and ar-AE traffic is isolated
/// (cookies, locale, dir attribute). Targets a *running* ESEMS dev server —
/// by default http://localhost:5297 (override with ESEMS_E2E_BASE_URL).
/// Tests SKIP gracefully if the server is not up OR if Playwright browsers
/// were never installed.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public string BaseUrl =>
        Environment.GetEnvironmentVariable("ESEMS_E2E_BASE_URL")?.TrimEnd('/')
        ?? "http://localhost:5297";

    public bool ServerReachable { get; private set; }

    /// <summary>
    /// Browser engine to launch. Set via env var ESEMS_E2E_BROWSER
    /// (chromium | firefox | webkit); defaults to chromium.
    /// Install browsers up-front with:
    ///   pwsh ESEMS.E2E\bin\Debug\net9.0\playwright.ps1 install chromium firefox webkit
    /// </summary>
    public string BrowserEngine =>
        (Environment.GetEnvironmentVariable("ESEMS_E2E_BROWSER") ?? "chromium").ToLowerInvariant();

    public async Task InitializeAsync()
    {
        try
        {
            Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            var launchOpts = new BrowserTypeLaunchOptions { Headless = true };
            Browser = BrowserEngine switch
            {
                "firefox" => await Playwright.Firefox.LaunchAsync(launchOpts),
                "webkit"  => await Playwright.Webkit.LaunchAsync(launchOpts),
                _         => await Playwright.Chromium.LaunchAsync(launchOpts),
            };
        }
        catch
        {
            // Browser not installed (`playwright install` never ran) — skip everything.
            ServerReachable = false;
            return;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var ping = await http.GetAsync($"{BaseUrl}/health");
            ServerReachable = ping.IsSuccessStatusCode;
        }
        catch
        {
            ServerReachable = false;
        }
    }

    public async Task<IBrowserContext> NewContextAsync(string locale)
    {
        return await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            Locale = locale,
            IgnoreHTTPSErrors = true,
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Cookie"] = locale.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
                    ? ".AspNetCore.Culture=c=ar|uic=ar"
                    : ".AspNetCore.Culture=c=en|uic=en",
            },
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.CloseAsync();
        Playwright?.Dispose();
    }
}
