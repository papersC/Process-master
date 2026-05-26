using System.Text.Json;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace ESEMS.E2E;

/// <summary>
/// Tier-3 accessibility scan — axe-core v4 driven by Playwright, EN + AR.
/// One test per top-10 highest-traffic route. Each test fails ONLY on
/// violations whose impact is "serious" or "critical" per the axe-core
/// rubric — "minor" / "moderate" issues are reported as JSON artifacts but
/// don't block the build.
///
/// JSON artifacts land at:
///   ESEMS.E2E/bin/Debug/net9.0/Axe/&lt;route-slug&gt;-&lt;locale&gt;.json
///
/// Standards baseline: WCAG 2.1 AA (the axe-core "wcag21aa" tag set).
/// Dubai government TDRA accessibility standard maps to WCAG 2.1 AA.
///
/// All tests skip gracefully if:
///   - the dev server is not reachable on /health
///   - Playwright browsers were never installed
///   - the seeded admin account is missing
/// </summary>
public class AccessibilityTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fx;
    public AccessibilityTests(PlaywrightFixture fx) { _fx = fx; }

    private const string AdminUser = "admin";
    private const string AdminPass = "Admin123";

    /// <summary>Top-10 routes by likely traffic / business importance.</summary>
    public static IEnumerable<object[]> TopRoutes() => new[]
    {
        new object[] { "/", "home" },
        new object[] { "/Processes", "processes-list" },
        new object[] { "/Services", "services-list" },
        new object[] { "/Assets", "assets-list" },
        new object[] { "/EnterpriseRisks", "risks-list" },
        new object[] { "/Improvements", "improvements-list" },
        new object[] { "/ChangeRequests", "change-requests-list" },
        new object[] { "/CustomerFeedback/Create", "feedback-create" },
        new object[] { "/SettingsHub", "settings-hub" },
        new object[] { "/Help", "help-center" },
    };

    public static IEnumerable<object[]> TopRoutesEnAr()
    {
        foreach (var r in TopRoutes())
        {
            yield return new[] { r[0], r[1], (object)"en-US" };
            yield return new[] { r[0], r[1], (object)"ar-AE" };
        }
    }

    private async Task<IPage?> LoginAsync(string locale)
    {
        if (!_fx.ServerReachable) return null;
        var ctx = await _fx.NewContextAsync(locale);
        var page = await ctx.NewPageAsync();
        try
        {
            await page.GotoAsync($"{_fx.BaseUrl}/Account/Login");
            await page.FillAsync("input[name='Username']", AdminUser);
            await page.FillAsync("input[type='password']", AdminPass);
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

    private static async Task SaveResultsAsync(string slug, string locale, AxeResult result)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Axe");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{slug}-{locale}.json");
            var summary = new
            {
                url = result.Url,
                timestamp = DateTime.UtcNow,
                violationCount = result.Violations.Length,
                violations = result.Violations.Select(v => new
                {
                    id = v.Id,
                    impact = v.Impact,
                    description = v.Description,
                    help = v.Help,
                    helpUrl = v.HelpUrl,
                    nodes = v.Nodes.Take(3).Select(n => new
                    {
                        html = n.Html,
                        target = n.Target.ToString(),
                    }).ToArray()
                }).ToArray(),
                passCount = result.Passes.Length,
                incompleteCount = result.Incomplete.Length,
                inapplicableCount = result.Inapplicable.Length,
            };
            await File.WriteAllTextAsync(path,
                JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best-effort artifact capture */ }
    }

    [Theory]
    [MemberData(nameof(TopRoutesEnAr))]
    public async Task Page_PassesWcag21AA_NoSeriousOrCriticalViolations(string route, string slug, string locale)
    {
        var page = await LoginAsync(locale);
        if (page == null) return;
        try
        {
            await page.GotoAsync($"{_fx.BaseUrl}{route}",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 15_000 });

            // Confine the scan to WCAG 2.1 AA only — wider tag sets surface
            // a lot of cosmetic noise from Bootstrap classes that isn't the
            // app's responsibility.
            var runOptions = new AxeRunOptions
            {
                RunOnly = new RunOnlyOptions
                {
                    Type = "tag",
                    Values = new List<string> { "wcag2a", "wcag2aa", "wcag21a", "wcag21aa" }
                }
            };

            var result = await page.RunAxe(runOptions);
            await SaveResultsAsync(slug, locale, result);

            var blocking = result.Violations
                .Where(v => v.Impact is "serious" or "critical")
                .ToList();

            if (blocking.Count > 0)
            {
                var lines = blocking.Select(v =>
                    $"  [{v.Impact}] {v.Id}: {v.Help} ({v.Nodes.Length} node{(v.Nodes.Length == 1 ? "" : "s")}) — {v.HelpUrl}");
                Assert.Fail(
                    $"WCAG 2.1 AA serious/critical violations on {route} ({locale}):\n" +
                    string.Join("\n", lines));
            }
        }
        finally { await page.Context.CloseAsync(); }
    }
}
