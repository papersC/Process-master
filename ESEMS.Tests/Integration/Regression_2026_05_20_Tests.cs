using System.Net;
using System.Net.Http.Headers;
using ESEMS.Web.Data;
using ESEMS.Web.Models;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.Improvement;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.AssetManagement;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ESEMS.Tests.Integration;

/// <summary>
/// Regression tests for the 19 fixes shipped on 2026-05-20 across commits
/// e56a908 → 36eef00 → 74d2512. Each test is named after the finding ID
/// from the bigTest audit so if it fails, the bug it was guarding against
/// has come back.
///
/// Covered (in order):
///   F-D-001   Assets Create/Edit no longer 500 on CustomUser.IsActive
///   F-CC-002  ChangeRequests Index accepts processId/serviceId filters
///   F-CC-002  ChangeRequests Create accepts processId/serviceId query
///   F-SV-001  ChangeRequest Details renders state-aware microcopy
///   F-CC-014  Layout double-submit guard script is on the page
///   CR state machine: StartReview / MarkImplemented / Cancel happy + illegal
/// </summary>
public class Regression_2026_05_20_Tests : IClassFixture<EsemsTestFactory>
{
    private readonly EsemsTestFactory _factory;
    public Regression_2026_05_20_Tests(EsemsTestFactory factory) { _factory = factory; }

    // ─────────────────────────────────────────────────────────────────────
    // Test client helpers
    // ─────────────────────────────────────────────────────────────────────

    private HttpClient AdminClient(bool allowRedirects = false)
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowRedirects });
        c.DefaultRequestHeaders.Add("X-Test-Role", "ADMIN");
        c.DefaultRequestHeaders.Add("X-Test-Perms", "*.*");
        c.DefaultRequestHeaders.Add("X-Test-UserId", "1");
        c.DefaultRequestHeaders.Add("X-Test-Name", "admin");
        return c;
    }

    // F-OP-004 / F-OP-005 (CustomerFeedback consent + PII masking) removed —
    // the CustomerFeedback UI (controller + views) no longer exists.

    // ─────────────────────────────────────────────────────────────────────
    // F-D-001 — Assets Create + Edit no longer 500 on CustomUser.IsActive
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FD001_AssetsCreate_DoesNotThrowOnCustomUserIsActive()
    {
        var client = AdminClient(allowRedirects: true);
        var resp = await client.GetAsync("/Assets/Create");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("DataOwnerUserId", html);
    }

    [Fact]
    public async Task FD001_AssetsEdit_DoesNotThrowOnCustomUserIsActive()
    {
        // Seed a minimal asset so /Assets/Edit/{id} has something to render.
        string id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var asset = new Asset
            {
                Id = Guid.NewGuid().ToString(),
                AssetTag = "AST-TEST-0001",
                NameEn = "Regression test asset",
                NameAr = "اختبار",
                CategoryId = (await db.AssetCategories.Where(c => !c.IsDeleted).Select(c => c.Id).FirstOrDefaultAsync())
                    ?? "00000000-0000-0000-0000-000000000000",
                Status = AssetStatus.Operational,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Assets.Add(asset);
            await db.SaveChangesAsync();
            id = asset.Id;
        }

        var client = AdminClient(allowRedirects: true);
        var resp = await client.GetAsync($"/Assets/Edit/{id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var html = await resp.Content.ReadAsStringAsync();
        // The <select asp-for="DataOwnerUserId"> renders to a <select> with id="DataOwnerUserId".
        Assert.Contains("DataOwnerUserId", html);
    }

    // ─────────────────────────────────────────────────────────────────────
    // F-CC-002 — ChangeRequests Index/Create accept processId / serviceId
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FCC002_ChangeRequests_Index_FiltersByProcessId()
    {
        string processId;
        string crIdInScope;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Two CRs: one with our processId, one without.
            processId = Guid.NewGuid().ToString();
            crIdInScope = Guid.NewGuid().ToString();
            db.ChangeRequests.Add(new ChangeRequest
            {
                Id = crIdInScope,
                Code = "CR-FILTER-A",
                NameEn = "Filtered CR",
                NameAr = "أ",
                ProcessId = processId,
                Status = ChangeRequestStatus.Submitted,
                Priority = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            });
            db.ChangeRequests.Add(new ChangeRequest
            {
                Id = Guid.NewGuid().ToString(),
                Code = "CR-FILTER-B",
                NameEn = "Unrelated CR",
                NameAr = "ب",
                Status = ChangeRequestStatus.Submitted,
                Priority = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            });
            await db.SaveChangesAsync();
        }

        var client = AdminClient(allowRedirects: true);
        var html = await client.GetStringAsync($"/ChangeRequests?processId={processId}");
        Assert.Contains("CR-FILTER-A", html);
        Assert.DoesNotContain("CR-FILTER-B", html);
    }

    [Fact]
    public async Task FCC002_ChangeRequests_Create_AcceptsProcessIdFromQuery()
    {
        // Seed a real process so the form-render path can find it; the FK
        // validator on Create may otherwise drop a non-existent value.
        string processId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var p = new ESEMS.Web.Models.APQC.Process
            {
                Id = Guid.NewGuid().ToString(),
                Code = "P-FCC002-TEST",
                NameEn = "FCC002 process",
                NameAr = "ع",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            db.Processes.Add(p);
            await db.SaveChangesAsync();
            processId = p.Id;
        }

        var client = AdminClient(allowRedirects: true);
        var resp = await client.GetAsync($"/ChangeRequests/Create?processId={processId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        // The form should render with a ProcessId binding present.
        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("ProcessId", html);
    }

    // ─────────────────────────────────────────────────────────────────────
    // F-SV-001 — ChangeRequest Details renders state-aware microcopy
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ChangeRequestStatus.Submitted,    "waiting for a reviewer")]
    [InlineData(ChangeRequestStatus.UnderReview,  "waiting for an approver")]
    [InlineData(ChangeRequestStatus.Approved,     "waiting for the implementer")]
    [InlineData(ChangeRequestStatus.Implemented,  "has been implemented")]
    [InlineData(ChangeRequestStatus.Rejected,     "was rejected")]
    [InlineData(ChangeRequestStatus.Cancelled,    "was cancelled")]
    public async Task FSV001_CRDetails_ShowsStateAwareMicrocopy(ChangeRequestStatus status, string expectedSnippet)
    {
        string id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            id = Guid.NewGuid().ToString();
            db.ChangeRequests.Add(new ChangeRequest
            {
                Id = id,
                Code = "CR-MICRO-" + status,
                NameEn = "Microcopy test",
                NameAr = "اختبار",
                Status = status,
                Priority = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            });
            await db.SaveChangesAsync();
        }

        var client = AdminClient(allowRedirects: true);
        var html = await client.GetStringAsync($"/ChangeRequests/Details/{id}");
        Assert.Contains("pc-state-hint", html);
        Assert.Contains(expectedSnippet, html);
    }

    // ─────────────────────────────────────────────────────────────────────
    // F-CC-014 — global double-submit guard script is on every layout page
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FCC014_DoubleSubmitGuard_PresentInLayout()
    {
        var client = AdminClient(allowRedirects: true);
        var html = await client.GetStringAsync("/Dashboard");
        Assert.Contains("F-CC-014", html);
        // Look for the actual guard behavior — disable on submit, 8s window.
        Assert.Contains("PENDING", html);
        Assert.Contains("data-allow-double-submit", html);
    }

    // ─────────────────────────────────────────────────────────────────────
    // CR lifecycle — happy + illegal transitions for the 3 new actions
    // (StartReview / MarkImplemented / Cancel) added today.
    // ─────────────────────────────────────────────────────────────────────

    private async Task<string> SeedCrAsync(ChangeRequestStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = Guid.NewGuid().ToString();
        db.ChangeRequests.Add(new ChangeRequest
        {
            Id = id,
            Code = "CR-LC-" + Guid.NewGuid().ToString("N")[..6],
            NameEn = "Lifecycle test",
            NameAr = "ل",
            Status = status,
            Priority = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<ChangeRequest> ReadCrAsync(string id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.ChangeRequests.FirstAsync(c => c.Id == id);
    }

    [Fact]
    public async Task CRLifecycle_StartReview_OnSubmitted_MovesToUnderReview()
    {
        var id = await SeedCrAsync(ChangeRequestStatus.Submitted);
        var client = AdminClient();
        var resp = await client.PostAsync($"/ChangeRequests/StartReview/{id}", new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));
        Assert.True(resp.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found);
        var cr = await ReadCrAsync(id);
        Assert.Equal(ChangeRequestStatus.UnderReview, cr.Status);
        Assert.NotNull(cr.ReviewStartedAt);
        Assert.Equal("admin", cr.ReviewStartedById);
    }

    [Fact]
    public async Task CRLifecycle_StartReview_OnApproved_IsRejected()
    {
        var id = await SeedCrAsync(ChangeRequestStatus.Approved);
        var client = AdminClient();
        await client.PostAsync($"/ChangeRequests/StartReview/{id}", new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));
        var cr = await ReadCrAsync(id);
        // Status must NOT have been bumped back to UnderReview.
        Assert.Equal(ChangeRequestStatus.Approved, cr.Status);
        Assert.Null(cr.ReviewStartedAt);
    }

    [Fact]
    public async Task CRLifecycle_MarkImplemented_OnApproved_MovesToImplemented()
    {
        var id = await SeedCrAsync(ChangeRequestStatus.Approved);
        var client = AdminClient();
        var resp = await client.PostAsync($"/ChangeRequests/MarkImplemented/{id}", new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));
        Assert.True(resp.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found);
        var cr = await ReadCrAsync(id);
        Assert.Equal(ChangeRequestStatus.Implemented, cr.Status);
        Assert.NotNull(cr.ImplementationDate);
        Assert.Equal("admin", cr.ImplementedById);
    }

    [Fact]
    public async Task CRLifecycle_MarkImplemented_OnSubmitted_IsRejected()
    {
        var id = await SeedCrAsync(ChangeRequestStatus.Submitted);
        var client = AdminClient();
        await client.PostAsync($"/ChangeRequests/MarkImplemented/{id}", new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));
        var cr = await ReadCrAsync(id);
        Assert.Equal(ChangeRequestStatus.Submitted, cr.Status);
        Assert.Null(cr.ImplementationDate);
    }

    [Theory]
    [InlineData(ChangeRequestStatus.Submitted)]
    [InlineData(ChangeRequestStatus.UnderReview)]
    [InlineData(ChangeRequestStatus.Approved)]
    public async Task CRLifecycle_Cancel_FromAnyNonTerminalState_MovesToCancelled(ChangeRequestStatus fromStatus)
    {
        var id = await SeedCrAsync(fromStatus);
        var client = AdminClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["cancellationReason"] = "deprioritised by leadership"
        });
        var resp = await client.PostAsync($"/ChangeRequests/Cancel/{id}", form);
        Assert.True(resp.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found);
        var cr = await ReadCrAsync(id);
        Assert.Equal(ChangeRequestStatus.Cancelled, cr.Status);
        Assert.NotNull(cr.CancelledAt);
        Assert.Equal("admin", cr.CancelledById);
        Assert.Equal("deprioritised by leadership", cr.CancellationReason);
    }

    [Theory]
    [InlineData(ChangeRequestStatus.Implemented)]
    [InlineData(ChangeRequestStatus.Rejected)]
    [InlineData(ChangeRequestStatus.Cancelled)]
    public async Task CRLifecycle_Cancel_FromTerminalState_IsRejected(ChangeRequestStatus fromStatus)
    {
        var id = await SeedCrAsync(fromStatus);
        var client = AdminClient();
        await client.PostAsync($"/ChangeRequests/Cancel/{id}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["cancellationReason"] = "should not work" }));
        var cr = await ReadCrAsync(id);
        // Must not have been re-cancelled or have its cancellation fields written.
        Assert.Equal(fromStatus, cr.Status);
        Assert.Null(cr.CancelledAt);
    }
}
