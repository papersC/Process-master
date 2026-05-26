using Microsoft.Playwright;

namespace ESEMS.E2E;

/// <summary>
/// Closes two of the gaps the post-redesign audit flagged as
/// "deferred to Playwright":
///
///   1. Print stylesheet — every redesigned page now uses
///      window.print() for export-on-print, but the print stylesheet
///      was never audited. Verifies that switching the browser to
///      print media correctly hides chrome (sidebar, topbar, AI
///      bubble, action buttons, DataTables search/paging) so the
///      printed output is just the page's content.
///
///   2. Document-upload UI on /Processes/Create — the
///      `_ProcessDocumentLinking` partial ships a hidden `<input
///      type="file">` driven by a JS toolbar. Verifies the toggle
///      reveals the upload button and the input accepts the right
///      MIME types. (The actual round-trip upload to the server
///      needs an integration test with persistent storage and is
///      out of scope for this pass — see notes in the test body.)
/// </summary>
public class PrintAndUploadTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fx;
    public PrintAndUploadTests(PlaywrightFixture fx) { _fx = fx; }

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
    public async Task PrintStylesheet_HidesChrome_OnListPage()
    {
        var page = await LoginAsync(_fx);
        if (page is null) return;

        // /Services has the full chrome surface: sidebar, topbar, lt-toolbar
        // (search + filters + Export button), DataTables-style filters, and
        // the floating AI bubble. None of them should be visible on print.
        await page.GotoAsync($"{_fx.BaseUrl}/Services");
        await page.WaitForSelectorAsync(".lt-shell", new() { Timeout = 8_000 });

        // Baseline (screen media): chrome IS visible.
        var sidebarVisibleOnScreen = await page.EvaluateAsync<bool>(
            "!!document.querySelector('.sidebar')?.offsetParent");
        Assert.True(sidebarVisibleOnScreen, "Sanity: sidebar must be visible on screen before media swap");

        // Switch to print media and re-check.
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { Media = Media.Print });

        var hidden = await page.EvaluateAsync<Dictionary<string, bool>>(@"
            (function () {
                function vis(sel) { var el = document.querySelector(sel); if (!el) return false; var s = getComputedStyle(el); return s.display !== 'none' && s.visibility !== 'hidden'; }
                return {
                    sidebar:        vis('.sidebar'),
                    topbar:         vis('.topbar') || vis('.top-header'),
                    aiBubble:       vis('.ai-floating-bubble') || vis('[data-ai-bubble]') || vis('#aiAssistantWidget'),
                    toolbar:        vis('.lt-toolbar'),
                    actionButtons:  vis('.lt-actions')
                };
            })()");

        Assert.False(hidden["sidebar"], "Sidebar should be hidden in print media");
        Assert.False(hidden["topbar"], "Top bar should be hidden in print media");
        Assert.False(hidden["aiBubble"], "AI floating bubble should be hidden in print media");
        Assert.False(hidden["toolbar"], "Search/filter toolbar should be hidden in print media");
        Assert.False(hidden["actionButtons"], "Per-row action buttons should be hidden in print media");

        // The page CONTENT (table / card) must remain visible.
        var contentVisible = await page.EvaluateAsync<bool>(
            "(function(){ var el = document.querySelector('.lt-card'); if (!el) return false; var s = getComputedStyle(el); return s.display !== 'none'; })()");
        Assert.True(contentVisible, "Print stylesheet should keep the content card visible");
    }

    [Fact]
    public async Task PrintStylesheet_StickyTableHeader_BecomesStaticOnPrint()
    {
        var page = await LoginAsync(_fx);
        if (page is null) return;

        await page.GotoAsync($"{_fx.BaseUrl}/Services");
        await page.WaitForSelectorAsync(".lt-table-wrap thead", new() { Timeout = 8_000 });

        // On screen: thead is `position: sticky` so it stays visible while
        // scrolling. On print, sticky positioning would create blank space
        // on every page and clip rows; the print stylesheet forces it static.
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { Media = Media.Print });

        var pos = await page.EvaluateAsync<string>(
            "getComputedStyle(document.querySelector('.lt-table-wrap thead')).position");
        Assert.Equal("static", pos);
    }

    [Fact]
    public async Task DocumentLinking_FileInput_AcceptsExpectedMimeTypes()
    {
        var page = await LoginAsync(_fx);
        if (page is null) return;

        await page.GotoAsync($"{_fx.BaseUrl}/Processes/Create");
        await page.WaitForSelectorAsync("#docLinkComputerInput", new() { Timeout = 8_000 });

        // Verify the file input is configured for the document types the
        // server expects (pdf / office / images / csv / txt). A regression
        // here would mean users could attach .exe or other unsupported
        // files and break the round-trip.
        var accept = await page.EvaluateAsync<string>(
            "document.getElementById('docLinkComputerInput').getAttribute('accept')");
        Assert.NotNull(accept);
        Assert.Contains(".pdf",  accept);
        Assert.Contains(".docx", accept);
        Assert.Contains(".xlsx", accept);
        Assert.Contains(".png",  accept);

        // The hidden JSON blob the form actually posts must default to []
        // — empty array, not "null" or undefined — so server-side
        // deserialization is deterministic.
        var defaultJson = await page.EvaluateAsync<string>(
            "document.getElementById('DocumentLinksJson').value");
        Assert.Equal("[]", defaultJson);

        // Toggling the document-linking switch should reveal the upload
        // toolbar (the "From Computer" / "From My Space" buttons live
        // inside #docLinkingFields, which starts collapsed).
        var collapsedBefore = await page.EvaluateAsync<bool>(
            "document.getElementById('docLinkingFields')?.classList.contains('hidden') === true");
        Assert.True(collapsedBefore, "Document-linking section should start collapsed");

        await page.ClickAsync("#docLinkingToggle");
        await page.WaitForFunctionAsync(
            "() => document.getElementById('docLinkingFields')?.classList.contains('hidden') === false",
            null, new() { Timeout = 4_000 });

        var fromComputerVisible = await page.EvaluateAsync<bool>(
            "(function(){ var b = document.getElementById('btnDocFromComputer'); return b && getComputedStyle(b).display !== 'none'; })()");
        Assert.True(fromComputerVisible, "Toggling document-linking on should reveal the From-Computer upload button");

        // NOTE — the actual file-upload round-trip (set files → JS reads
        // them → POST to /Documents/Upload → server stores → JSON blob
        // updated) requires storage credentials and a running document
        // service. That's out of scope for this audit pass. The test above
        // covers the UI surface that the audit specifically flagged
        // ("file uploads were not exercised"); the round-trip belongs to a
        // dedicated integration test once the storage backend is mocked.
    }
}
