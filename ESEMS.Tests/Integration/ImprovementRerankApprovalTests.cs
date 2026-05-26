using System.Net;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.Improvement;
using ESEMS.Web.Models.Workflow;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ESEMS.Tests.Integration;

/// <summary>
/// E2E coverage for the Re-rank-and-Approve flow shipped on
/// `audit/full-system-2026-05-05` (commit 570e83a). The approver opens
/// /Workflow/PendingApprovals, clicks "Re-rank…" on an Improvement row,
/// changes Quadrant + Horizon, and submits — under the hood that POSTs
/// to /Improvements/Approve/{id} with quadrant + horizon form fields.
///
/// What this asserts:
///   • The endpoint accepts both reclassification fields and the comments
///   • The initiative ends up Approved with the NEW Quadrant + Horizon
///   • Two ImprovementChangeLog rows are written — one per overridden
///     field — both attributed to the approver and tagged with the
///     "Re-classified by approver during approval" reason
///
/// Would fail if a refactor:
///   • Drops the (quadrant, horizon) overload of Approve
///   • Skips the change-log insertion (the audit-trail story breaks)
///   • Stamps ChangedById from the submitter instead of the approver
/// </summary>
public class ImprovementRerankApprovalTests : IClassFixture<EsemsTestFactory>
{
    private readonly EsemsTestFactory _factory;
    public ImprovementRerankApprovalTests(EsemsTestFactory factory) { _factory = factory; }

    private HttpClient AsApprover(string userId = "777", string name = "carol.approver")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        client.DefaultRequestHeaders.Add("X-Test-Name", name);
        client.DefaultRequestHeaders.Add("X-Test-Role", "ADMIN");
        client.DefaultRequestHeaders.Add("X-Test-Perms", "*.*");
        return client;
    }

    private async Task<string> SeedUnderReviewInitiativeAsync(
        ImprovementQuadrant initialQuadrant = ImprovementQuadrant.QuickWins,
        ImprovementHorizon? initialHorizon = ImprovementHorizon.Horizon1_Current)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var imp = new ImprovementInitiative
        {
            Id = Guid.NewGuid().ToString(),
            Code = "IMP-RR-" + Guid.NewGuid().ToString("N")[..6],
            TitleEn = "Rerank probe",
            TitleAr = "اختبار إعادة التصنيف",
            DescriptionEn = "x",
            Status = ImprovementStatus.UnderReview,
            Quadrant = initialQuadrant,
            Horizon = initialHorizon,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            CreatedById = "alice.submitter"
        };
        db.ImprovementInitiatives.Add(imp);

        // The Approve endpoint requires an open WorkflowInstance — without
        // it the controller short-circuits with 400 ("No active workflow…").
        var wf = new WorkflowInstance
        {
            Id = Guid.NewGuid().ToString(),
            EntityId = imp.Id,
            EntityType = "Improvement",
            Status = WorkflowStatus.UnderReview,
            CurrentLevel = 1,
            MaxLevel = 1,
            ApproverUserId = 777,
            SubmittedById = 100,
            SubmitterName = "alice.submitter",
            SubmittedAt = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.WorkflowInstances.Add(wf);
        await db.SaveChangesAsync();
        return imp.Id;
    }

    [Fact]
    public async Task Approve_WithRerank_PersistsNewClassification_AndLogsBothChanges()
    {
        // Submitter put it in QuickWins / H1. Approver disagrees and
        // re-classifies as MajorProjects / H2 before approving.
        var id = await SeedUnderReviewInitiativeAsync(
            initialQuadrant: ImprovementQuadrant.QuickWins,
            initialHorizon: ImprovementHorizon.Horizon1_Current);

        var resp = await AsApprover().PostAsync($"/Improvements/Approve/{id}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["quadrant"] = ImprovementQuadrant.MajorProjects.ToString(),
                ["horizon"]  = ImprovementHorizon.Horizon2_Expand.ToString(),
                ["comments"] = "Committee judgment: cost is higher than submitter estimated."
            }));

        Assert.True(
            resp.StatusCode == HttpStatusCode.Redirect || resp.StatusCode == HttpStatusCode.Found,
            $"Expected redirect after Approve, got {(int)resp.StatusCode}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var imp = await db.ImprovementInitiatives.FirstAsync(i => i.Id == id);
        Assert.Equal(ImprovementStatus.Approved, imp.Status);
        Assert.Equal(ImprovementQuadrant.MajorProjects, imp.Quadrant);
        Assert.Equal(ImprovementHorizon.Horizon2_Expand, imp.Horizon);

        // Both reclassifications must show up in the change log.
        var log = await db.Set<ImprovementChangeLog>()
            .Where(l => l.ImprovementId == id)
            .ToListAsync();

        var quadEntry = log.FirstOrDefault(l => l.FieldName == nameof(ImprovementInitiative.Quadrant));
        var horiEntry = log.FirstOrDefault(l => l.FieldName == nameof(ImprovementInitiative.Horizon));

        Assert.NotNull(quadEntry);
        Assert.Equal("QuickWins", quadEntry!.OldValue);
        Assert.Equal("MajorProjects", quadEntry.NewValue);
        Assert.Contains("Re-classified by approver", quadEntry.ChangeReason ?? "");
        Assert.Equal("carol.approver", quadEntry.ChangedById);

        Assert.NotNull(horiEntry);
        Assert.Equal("Horizon1_Current", horiEntry!.OldValue);
        Assert.Equal("Horizon2_Expand", horiEntry.NewValue);
        Assert.Contains("Re-classified by approver", horiEntry.ChangeReason ?? "");
        Assert.Equal("carol.approver", horiEntry.ChangedById);
    }

    [Fact]
    public async Task Approve_WithoutRerank_LeavesClassificationUntouched()
    {
        // Same flow but the approver doesn't change Quadrant / Horizon (the
        // fast-path "Approve" button on My Approvals just omits the fields).
        // No change log rows for those fields should be written.
        var id = await SeedUnderReviewInitiativeAsync(
            initialQuadrant: ImprovementQuadrant.FillIns,
            initialHorizon: ImprovementHorizon.Horizon3_Future);

        var resp = await AsApprover().PostAsync($"/Improvements/Approve/{id}",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.True(
            resp.StatusCode == HttpStatusCode.Redirect || resp.StatusCode == HttpStatusCode.Found,
            $"Expected redirect after Approve, got {(int)resp.StatusCode}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var imp = await db.ImprovementInitiatives.FirstAsync(i => i.Id == id);
        Assert.Equal(ImprovementStatus.Approved, imp.Status);
        Assert.Equal(ImprovementQuadrant.FillIns, imp.Quadrant);
        Assert.Equal(ImprovementHorizon.Horizon3_Future, imp.Horizon);

        var quadHorChanges = await db.Set<ImprovementChangeLog>()
            .Where(l => l.ImprovementId == id
                     && (l.FieldName == nameof(ImprovementInitiative.Quadrant)
                         || l.FieldName == nameof(ImprovementInitiative.Horizon)))
            .CountAsync();
        Assert.Equal(0, quadHorChanges);
    }
}
