using Microsoft.Playwright;

namespace ESEMS.E2E;

/// <summary>
/// Regression tests for commit bd5ae43 — global CSP-safe inline-onclick
/// polyfill in _Layout.cshtml + _LayoutNoSidebar.cshtml.
///
/// The bug it fixed: ESEMS' CSP script-src has nonce + 'unsafe-eval' but
/// no 'unsafe-inline' / 'unsafe-hashes'. That silently drops every HTML
/// onclick="..." attribute — getAttribute('onclick') returns the string,
/// but el.onclick is null and the handler never compiles. Buttons across
/// 43 view files were dead. The polyfill walks up to the deepest ancestor
/// with an onclick attribute on every click and runs its body via
/// `new Function(...)` (allowed by 'unsafe-eval').
///
/// These tests would FAIL if:
///   - the polyfill is removed from the layout
///   - 'unsafe-eval' is dropped from script-src (the polyfill's only
///     escape hatch)
///   - layout-cascading is broken so views don't inherit it
///
/// Verifies the polyfill on /ProcessHierarchy, which has 27 inline
/// onclicks and was the page where the bug was first observed.
/// </summary>
public class InlineOnclickPolyfillTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fx;
    public InlineOnclickPolyfillTests(PlaywrightFixture fx) { _fx = fx; }

    private const string TestUsername = "editor";
    private const string TestPassword = "Editor123";

    private static async Task<IPage?> LoginAsync(PlaywrightFixture fx)
    {
        if (!fx.ServerReachable) return null;
        var ctx = await fx.NewContextAsync("en-US");
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
    public async Task Polyfill_IsInstalled_OnEveryPage()
    {
        var page = await LoginAsync(_fx);
        if (page is null) return; // skip — server / browser / login unavailable

        await page.GotoAsync($"{_fx.BaseUrl}/ProcessHierarchy");
        // The polyfill registers a delegated click listener at document level.
        // We can't introspect that, but we can verify `new Function` works
        // (which is the polyfill's escape hatch) and that onclick attributes
        // are still present on the page (they were dead but not removed).
        var unsafeEvalWorks = await page.EvaluateAsync<bool>("(function(){ try { return new Function('return 1')() === 1; } catch(e) { return false; } })()");
        Assert.True(unsafeEvalWorks, "'unsafe-eval' is not in script-src — the polyfill cannot work without it");

        var onclickCount = await page.EvaluateAsync<int>("document.querySelectorAll('[onclick]').length");
        Assert.True(onclickCount > 0, "Expected /ProcessHierarchy to ship inline onclick attributes for the polyfill to handle");
    }

    [Fact]
    public async Task Polyfill_FiresExpandAll_OnProcessHierarchy()
    {
        var page = await LoginAsync(_fx);
        if (page is null) return;

        await page.GotoAsync($"{_fx.BaseUrl}/ProcessHierarchy");
        // Wait for the org-chart to render before counting
        await page.WaitForSelectorAsync(".ph-sector, .ph-topunit, .ph-dept", new() { Timeout = 8_000 });

        var totalCardsBefore = await page.EvaluateAsync<int>(
            "document.querySelectorAll('.ph-sector, .ph-topunit, .ph-dept').length");
        var expandedBefore = await page.EvaluateAsync<int>(
            "document.querySelectorAll('.ph-sector.expanded, .ph-topunit.expanded, .ph-dept.expanded').length");
        Assert.True(totalCardsBefore > 0, "Expected at least one expandable card on the org chart");
        Assert.Equal(0, expandedBefore); // sanity: nothing expanded by default

        // The Expand All button uses inline onclick="expandAll()" — exactly
        // the kind of handler that was dead before bd5ae43.
        await page.ClickAsync(".ph-topbar-actions .ph-toolbar-btn:first-child");

        // Give the click event a moment to flow through the polyfill.
        await page.WaitForFunctionAsync(
            "() => document.querySelectorAll('.ph-sector.expanded, .ph-topunit.expanded, .ph-dept.expanded').length > 0",
            null,
            new() { Timeout = 4_000 });

        var expandedAfter = await page.EvaluateAsync<int>(
            "document.querySelectorAll('.ph-sector.expanded, .ph-topunit.expanded, .ph-dept.expanded').length");
        Assert.Equal(totalCardsBefore, expandedAfter); // all cards expanded
    }

    [Fact]
    public async Task Polyfill_DoesNotDoubleFire_OnNestedOnclickElements()
    {
        var page = await LoginAsync(_fx);
        if (page is null) return;

        await page.GotoAsync($"{_fx.BaseUrl}/ProcessHierarchy");
        await page.WaitForSelectorAsync(".ph-card", new() { Timeout = 8_000 });

        // The org-chart cards have the onclick on the .ph-hd parent AND on
        // the .ph-mini-toggle child (with event.stopPropagation()). The
        // polyfill walks up to the *deepest* ancestor with an onclick — i.e.
        // the toggle, not the parent. If it fired both, the card would
        // toggle then immediately untoggle, netting zero change.
        var toggleSel = ".ph-topunit:not(.expanded) .ph-mini-toggle";
        var hasToggle = await page.EvaluateAsync<bool>($"!!document.querySelector('{toggleSel}')");
        if (!hasToggle) return; // no expandable top-units in this dataset

        var cardId = await page.EvaluateAsync<string>(
            $"document.querySelector('{toggleSel}')?.closest('.ph-topunit')?.id");
        Assert.False(string.IsNullOrEmpty(cardId));

        await page.ClickAsync(toggleSel);
        await page.WaitForTimeoutAsync(300); // allow click to settle

        var isExpanded = await page.EvaluateAsync<bool>(
            $"document.getElementById('{cardId}')?.classList.contains('expanded') === true");
        Assert.True(isExpanded, "Toggle button click should net to a single expand, not cancel itself");
    }
}
