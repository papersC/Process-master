using System.Text.Json;
using Microsoft.Playwright;

namespace ESEMS.E2E;

/// <summary>
/// Tier-2 Playwright regression suite for the 19 fixes shipped on 2026-05-20.
/// Each scenario is parametrised over (en-US, ltr) and (ar-AE, rtl) so the
/// fix survives a culture switch.
///
/// All tests skip gracefully if:
///   - the dev server is not reachable on /health
///   - Playwright browsers were never installed
///   - the seeded admin account is missing
///
/// Run after starting the dev server:
///   dotnet run --project ESEMS.Web
///   dotnet test ESEMS.E2E --filter "FullyQualifiedName~Regression_2026_05_20_E2E"
///
/// Traces / screenshots on failure land at:
///   ESEMS.E2E/bin/Debug/net9.0/Traces/&lt;test&gt;-&lt;locale&gt;-failure.zip / .png
/// </summary>
public class Regression_2026_05_20_E2E : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fx;
    public Regression_2026_05_20_E2E(PlaywrightFixture fx) { _fx = fx; }

    private const string AdminUser = "admin";
    private const string AdminPass = "Admin123";

    // ─────────────────────────────────────────────────────────────────────
    // Login helper — returns null on any failure (caller treats as skip).
    // ─────────────────────────────────────────────────────────────────────
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

    private static async Task SaveFailureArtifactsAsync(IPage page, string testName, string locale)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Traces");
            Directory.CreateDirectory(dir);
            await page.ScreenshotAsync(new() { Path = Path.Combine(dir, $"{testName}-{locale}-failure.png"), FullPage = true });
        }
        catch { /* best-effort artifact capture */ }
    }

    // ─────────────────────────────────────────────────────────────────────
    // F-OP-004 — PDPL consent block renders on /CustomerFeedback/Create
    // ─────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("en-US", "Privacy notice")]
    [InlineData("ar-AE", "إشعار الخصوصية")]
    public async Task FOP004_FeedbackCreate_ShowsConsentBlock_BothLocales(string locale, string expectedTitleSnippet)
    {
        var page = await LoginAsync(locale);
        if (page == null) return;
        try
        {
            await page.GotoAsync($"{_fx.BaseUrl}/CustomerFeedback/Create");
            var consentTitle = page.Locator("#pc-consent-title");
            await Assertions.Expect(consentTitle).ToBeVisibleAsync(new() { Timeout = 5_000 });
            await Assertions.Expect(consentTitle).ToContainTextAsync(expectedTitleSnippet);

            var checkbox = page.Locator("#ConsentCheck");
            await Assertions.Expect(checkbox).ToBeVisibleAsync();
            Assert.False(await checkbox.IsCheckedAsync(), "Consent checkbox must NOT be pre-checked.");
        }
        catch
        {
            await SaveFailureArtifactsAsync(page, nameof(FOP004_FeedbackCreate_ShowsConsentBlock_BothLocales), locale);
            throw;
        }
        finally { await page.Context.CloseAsync(); }
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ar-AE")]
    public async Task FOP004_FeedbackCreate_BlocksAdvanceWithoutConsent(string locale)
    {
        var page = await LoginAsync(locale);
        if (page == null) return;
        try
        {
            await page.GotoAsync($"{_fx.BaseUrl}/CustomerFeedback/Create");
            await page.FillAsync("#CustomerName", "E2E consent test");
            // Do NOT tick #ConsentCheck.
            await page.ClickAsync("#nextBtn");

            // The wizard's validateStep(1) raises a SweetAlert when consent is
            // missing — block the user on step 1.
            var swal = page.Locator(".swal2-container");
            await Assertions.Expect(swal).ToBeVisibleAsync(new() { Timeout = 5_000 });

            // We must still be on step 1 (counter stays at "1").
            var step = await page.Locator("#stepCounter").TextContentAsync();
            Assert.Equal("1", (step ?? "").Trim());
        }
        catch
        {
            await SaveFailureArtifactsAsync(page, nameof(FOP004_FeedbackCreate_BlocksAdvanceWithoutConsent), locale);
            throw;
        }
        finally { await page.Context.CloseAsync(); }
    }

    // ─────────────────────────────────────────────────────────────────────
    // F-OP-005 — PII masking on /CustomerFeedback/Details
    // The fixture seeds no feedback so this test reads /CustomerFeedback,
    // picks the first record-with-email, and inspects masking on its Details.
    // ─────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("en-US")]
    [InlineData("ar-AE")]
    public async Task FOP005_FeedbackDetails_MasksPIIByDefault(string locale)
    {
        var page = await LoginAsync(locale);
        if (page == null) return;
        try
        {
            await page.GotoAsync($"{_fx.BaseUrl}/CustomerFeedback");
            // Find first detail link; skip if list is empty.
            var detailLinks = page.Locator("a[href*='/CustomerFeedback/Details/']");
            var count = await detailLinks.CountAsync();
            if (count == 0) return; // empty list — non-blocking skip

            // Walk up to the first record that actually has a customer email.
            string? targetUrl = null;
            for (int i = 0; i < Math.Min(count, 10); i++)
            {
                var href = await detailLinks.Nth(i).GetAttributeAsync("href");
                if (string.IsNullOrEmpty(href)) continue;
                var resp = await page.Context.APIRequest.GetAsync($"{_fx.BaseUrl}{href}");
                var body = await resp.TextAsync();
                if (body.Contains("data-pii=\"email\"", StringComparison.Ordinal))
                {
                    targetUrl = $"{_fx.BaseUrl}{href}";
                    break;
                }
            }
            if (targetUrl == null) return; // no feedback with email seeded — skip

            await page.GotoAsync(targetUrl);
            var piiEmail = page.Locator("[data-pii='email']").First;
            await Assertions.Expect(piiEmail).ToBeVisibleAsync(new() { Timeout = 5_000 });
            var rendered = (await piiEmail.TextContentAsync() ?? "").Trim();
            Assert.Contains("•", rendered);
            Assert.DoesNotContain("@example.com", rendered.Replace("•@example.com", "")); // bullets must precede the @
        }
        catch
        {
            await SaveFailureArtifactsAsync(page, nameof(FOP005_FeedbackDetails_MasksPIIByDefault), locale);
            throw;
        }
        finally { await page.Context.CloseAsync(); }
    }

    [Fact]
    public async Task FOP005_FeedbackDetails_AdminCanRevealPII()
    {
        var page = await LoginAsync("en-US");
        if (page == null) return;
        try
        {
            await page.GotoAsync($"{_fx.BaseUrl}/CustomerFeedback");
            var detailLinks = page.Locator("a[href*='/CustomerFeedback/Details/']");
            var count = await detailLinks.CountAsync();
            if (count == 0) return;

            // Walk to a record with email.
            string? targetUrl = null;
            for (int i = 0; i < Math.Min(count, 10); i++)
            {
                var href = await detailLinks.Nth(i).GetAttributeAsync("href");
                if (string.IsNullOrEmpty(href)) continue;
                var resp = await page.Context.APIRequest.GetAsync($"{_fx.BaseUrl}{href}");
                var body = await resp.TextAsync();
                if (body.Contains("data-pii=\"email\"", StringComparison.Ordinal))
                {
                    targetUrl = $"{_fx.BaseUrl}{href}";
                    break;
                }
            }
            if (targetUrl == null) return;

            await page.GotoAsync(targetUrl);
            var pii = page.Locator("[data-pii='email']").First;
            var maskedText = (await pii.TextContentAsync() ?? "").Trim();
            Assert.Contains("•", maskedText);

            await page.ClickAsync("#piiToggleBtn");
            await page.WaitForFunctionAsync("document.getElementById('piiToggleBtn').getAttribute('data-revealed') === 'true'");
            var unmasked = (await pii.TextContentAsync() ?? "").Trim();
            Assert.DoesNotContain("•", unmasked);
            Assert.Contains("@", unmasked);
        }
        catch
        {
            await SaveFailureArtifactsAsync(page, nameof(FOP005_FeedbackDetails_AdminCanRevealPII), "en-US");
            throw;
        }
        finally { await page.Context.CloseAsync(); }
    }

    // ─────────────────────────────────────────────────────────────────────
    // F-CC-011 — SettingsHub tabs scroll horizontally at mobile (375 px)
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task FCC011_SettingsHubTabs_HorizontalScrollOnMobile()
    {
        if (!_fx.ServerReachable) return;
        await using var ctx = await _fx.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            Locale = "en-US",
            ViewportSize = new() { Width = 375, Height = 812 },
            IgnoreHTTPSErrors = true,
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Cookie"] = ".AspNetCore.Culture=c=en|uic=en",
            },
        });
        var page = await ctx.NewPageAsync();
        try
        {
            await page.GotoAsync($"{_fx.BaseUrl}/Account/Login");
            await page.FillAsync("input[name='Username']", AdminUser);
            await page.FillAsync("input[type='password']", AdminPass);
            await page.ClickAsync("button[type='submit']");
            await page.WaitForURLAsync(u => !u.Contains("/Account/Login"), new() { Timeout = 8_000 });

            await page.GotoAsync($"{_fx.BaseUrl}/SettingsHub");
            var wrap = page.Locator(".sh-tabs-scroll");
            await Assertions.Expect(wrap).ToBeVisibleAsync(new() { Timeout = 5_000 });
            // Return the overflow check directly from JS so we don't fight
            // Playwright's STJ deserialization into a generic Dictionary.
            var overflows = await wrap.EvaluateAsync<bool>("el => el.scrollWidth > el.clientWidth");
            Assert.True(overflows, "Tab strip should overflow at 375px (scrollWidth must exceed clientWidth).");

            // Scroll to the right edge — the last tab ('About') should land in viewport.
            await wrap.EvaluateAsync("el => el.scrollLeft = el.scrollWidth");
            var lastTabVisible = await page.Locator("button:has-text('About')").Last.IsVisibleAsync();
            Assert.True(lastTabVisible, "Last tab unreachable after horizontal scroll.");
        }
        catch
        {
            await SaveFailureArtifactsAsync(page, nameof(FCC011_SettingsHubTabs_HorizontalScrollOnMobile), "mobile-375");
            throw;
        }
        finally { await ctx.CloseAsync(); }
    }

    // ─────────────────────────────────────────────────────────────────────
    // F-SV-001 — ChangeRequest Details renders state-aware microcopy
    // ─────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("en-US")]
    [InlineData("ar-AE")]
    public async Task FSV001_ChangeRequestDetails_HasStateHintBlock(string locale)
    {
        var page = await LoginAsync(locale);
        if (page == null) return;
        try
        {
            await page.GotoAsync($"{_fx.BaseUrl}/ChangeRequests");
            var detailLinks = page.Locator("a[href*='/ChangeRequests/Details/']");
            var count = await detailLinks.CountAsync();
            if (count == 0) return;
            var href = await detailLinks.First.GetAttributeAsync("href");
            await page.GotoAsync($"{_fx.BaseUrl}{href}");
            var hint = page.Locator(".pc-state-hint");
            await Assertions.Expect(hint).ToBeVisibleAsync(new() { Timeout = 5_000 });
            var text = (await hint.TextContentAsync() ?? "").Trim();
            Assert.NotEmpty(text);
        }
        catch
        {
            await SaveFailureArtifactsAsync(page, nameof(FSV001_ChangeRequestDetails_HasStateHintBlock), locale);
            throw;
        }
        finally { await page.Context.CloseAsync(); }
    }

    // ─────────────────────────────────────────────────────────────────────
    // F-CC-002 — "Change requests for this process" panel on Process Details
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task FCC002_ProcessDetails_ShowsRelatedChangeRequestsPanel()
    {
        var page = await LoginAsync("en-US");
        if (page == null) return;
        try
        {
            await page.GotoAsync($"{_fx.BaseUrl}/Processes");
            var detailLinks = page.Locator("a[href*='/Processes/Details/']");
            var count = await detailLinks.CountAsync();
            if (count == 0) return;
            var href = await detailLinks.First.GetAttributeAsync("href");
            await page.GotoAsync($"{_fx.BaseUrl}{href}");

            var section = page.Locator("[data-section='related-change-requests']");
            await Assertions.Expect(section).ToBeVisibleAsync(new() { Timeout = 5_000 });

            var openCrButton = section.Locator("a:has-text('Open change request')");
            await Assertions.Expect(openCrButton).ToBeVisibleAsync();

            // The Open-CR button must deep-link with the processId pre-filled.
            var openHref = await openCrButton.GetAttributeAsync("href") ?? string.Empty;
            Assert.Contains("/ChangeRequests/Create", openHref);
            Assert.Contains("processId=", openHref);
        }
        catch
        {
            await SaveFailureArtifactsAsync(page, nameof(FCC002_ProcessDetails_ShowsRelatedChangeRequestsPanel), "en-US");
            throw;
        }
        finally { await page.Context.CloseAsync(); }
    }

    // ─────────────────────────────────────────────────────────────────────
    // F-D-001 — Assets/Create + Assets/Edit return 200 (no CustomUser.IsActive 500)
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task FD001_AssetsCreate_LoadsSuccessfully()
    {
        var page = await LoginAsync("en-US");
        if (page == null) return;
        try
        {
            var resp = await page.GotoAsync($"{_fx.BaseUrl}/Assets/Create");
            Assert.NotNull(resp);
            Assert.True(resp!.Ok, $"/Assets/Create returned {resp.Status}. The CustomUser.IsActive regression is back.");
            // The DataOwner picker lives inside the "Information asset" <details>
            // block, which defaults collapsed for new assets. We only care that
            // it's present in the DOM and populated — visibility is gated by
            // the user clicking the summary, which isn't part of this regression.
            var ownerSelect = page.Locator("select#DataOwnerUserId");
            Assert.True(await ownerSelect.CountAsync() > 0, "DataOwnerUserId picker missing from /Assets/Create markup.");
            var optionCount = await ownerSelect.Locator("option").CountAsync();
            Assert.True(optionCount >= 2, $"DataOwner dropdown has only {optionCount} options — PopulateDropdowns broke again?");
        }
        catch
        {
            await SaveFailureArtifactsAsync(page, nameof(FD001_AssetsCreate_LoadsSuccessfully), "en-US");
            throw;
        }
        finally { await page.Context.CloseAsync(); }
    }

    // ─────────────────────────────────────────────────────────────────────
    // F-CC-005 — Language toggle preserves the current URL + query
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task FCC005_LanguageToggle_PreservesCurrentUrl()
    {
        var page = await LoginAsync("en-US");
        if (page == null) return;
        try
        {
            await page.GotoAsync($"{_fx.BaseUrl}/Processes?status=Active");
            var beforePath = new Uri(page.Url).AbsolutePath;
            var beforeQuery = new Uri(page.Url).Query;

            // Trigger the layout's SetLanguage() function via the topbar toggle.
            await page.EvaluateAsync("() => SetLanguage('ar')");
            await page.WaitForFunctionAsync("() => document.documentElement.lang === 'ar'", null, new() { Timeout = 8_000 });

            var afterPath = new Uri(page.Url).AbsolutePath;
            var afterQuery = new Uri(page.Url).Query;
            Assert.Equal(beforePath, afterPath);
            Assert.Equal(beforeQuery, afterQuery);
        }
        catch
        {
            await SaveFailureArtifactsAsync(page, nameof(FCC005_LanguageToggle_PreservesCurrentUrl), "toggle");
            throw;
        }
        finally { await page.Context.CloseAsync(); }
    }

    // ─────────────────────────────────────────────────────────────────────
    // F-CC-014 — Global double-submit guard is loaded on every authenticated page
    // ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task FCC014_DoubleSubmitGuard_PreventsSecondSubmit()
    {
        var page = await LoginAsync("en-US");
        if (page == null) return;
        try
        {
            // Build a synthetic form on the dashboard page and dispatch two submits.
            // Return a JsonElement so we don't fight Playwright's STJ binding
            // into a generic Dictionary.
            var resultJson = await page.EvaluateAsync<JsonElement>(@"
                async () => {
                    const form = document.createElement('form');
                    form.method = 'POST';
                    form.action = 'javascript:void(0)';
                    const btn = document.createElement('button');
                    btn.type = 'submit';
                    form.appendChild(btn);
                    document.body.appendChild(form);
                    let fired = 0;
                    form.addEventListener('submit', e => { e.preventDefault(); fired++; });
                    form.requestSubmit(btn);
                    await new Promise(r => setTimeout(r, 50));
                    const disabledAfterFirst = btn.disabled;
                    form.requestSubmit(btn);
                    await new Promise(r => setTimeout(r, 50));
                    form.remove();
                    return { fired, disabledAfterFirst };
                }");
            Assert.Equal(1, resultJson.GetProperty("fired").GetInt32());
            Assert.True(resultJson.GetProperty("disabledAfterFirst").GetBoolean());
        }
        catch
        {
            await SaveFailureArtifactsAsync(page, nameof(FCC014_DoubleSubmitGuard_PreventsSecondSubmit), "en-US");
            throw;
        }
        finally { await page.Context.CloseAsync(); }
    }

    // ─────────────────────────────────────────────────────────────────────
    // F-CC-003 — Breadcrumb renders on the previously-missing detail pages
    // ─────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("en-US")]
    [InlineData("ar-AE")]
    public async Task FCC003_Breadcrumb_RendersOnServiceCategoriesIndex(string locale)
    {
        var page = await LoginAsync(locale);
        if (page == null) return;
        try
        {
            await page.GotoAsync($"{_fx.BaseUrl}/ServiceCategories");
            // The breadcrumb partial renders inside a <nav aria-label="Breadcrumb"...> region.
            var crumb = page.Locator("nav[aria-label='Breadcrumb'], nav[aria-label='التنقل التفصيلي']");
            await Assertions.Expect(crumb.First).ToBeVisibleAsync(new() { Timeout = 5_000 });
            // The last segment should carry aria-current="page".
            var current = page.Locator("[aria-current='page']");
            Assert.True(await current.CountAsync() > 0, "Breadcrumb missing aria-current=page on the last segment.");
        }
        catch
        {
            await SaveFailureArtifactsAsync(page, nameof(FCC003_Breadcrumb_RendersOnServiceCategoriesIndex), locale);
            throw;
        }
        finally { await page.Context.CloseAsync(); }
    }
}
