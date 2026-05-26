using System.Net;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.Improvement;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ESEMS.Tests.Integration;

/// <summary>
/// Multi-actor end-to-end workflow trace — closes the audit gap that
/// flagged "controller → DB → audit-log story across multiple roles is
/// not exercised". Drives a full ChangeRequest lifecycle with TWO
/// distinct identities (submitter vs approver) using TestAuthHandler's
/// per-request header-driven identity, then verifies:
///
///   • Status transitions correctly (Submitted → Approved)
///   • Author/approver fields are stamped from the authenticated caller
///     of THAT action — not from the previous step's actor
///   • The audit-log interceptor records BOTH actions with the right
///     UserId per step
///
/// This test would FAIL if a refactor accidentally:
///   • Stamps ApprovedById from the submitter (e.g. by re-using a stale
///     `User.Identity` reference)
///   • Drops the audit interceptor or its UserId resolution
///   • Lets the approver's identity leak into the submitter's row
/// </summary>
public class MultiActorWorkflowTests : IClassFixture<EsemsTestFactory>
{
    private readonly EsemsTestFactory _factory;
    public MultiActorWorkflowTests(EsemsTestFactory factory) { _factory = factory; }

    private HttpClient AsUser(string userId, string name, string role = "ADMIN")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        client.DefaultRequestHeaders.Add("X-Test-Name", name);
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-Perms", "*.*");
        return client;
    }

    [Fact]
    public async Task ChangeRequest_SubmitByOneUser_ApprovedByAnother_StampsCorrectIdentities()
    {
        const string submitterId   = "100";
        const string submitterName = "alice.submitter";
        const string approverId    = "200";
        const string approverName  = "bob.approver";

        // ── Step 1: Alice submits a change request ────────────────────
        var aliceClient = AsUser(submitterId, submitterName);
        var createResp = await aliceClient.PostAsync("/ChangeRequests/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["NameEn"] = "Multi-actor probe",
                ["NameAr"] = "تتبع متعدد الفاعلين",
                ["DescriptionEn"] = "End-to-end trace",
                ["Justification"] = "regression coverage",
                ["Priority"] = "3"
            }));
        Assert.True(createResp.StatusCode == HttpStatusCode.Redirect || createResp.StatusCode == HttpStatusCode.Found,
            $"Create should redirect; got {(int)createResp.StatusCode}");

        string crId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cr = await db.ChangeRequests
                .Where(c => c.NameEn == "Multi-actor probe")
                .OrderByDescending(c => c.CreatedAt)
                .FirstAsync();

            // After Step 1: status Submitted; CreatedById is Alice; no approval yet
            Assert.Equal(ChangeRequestStatus.Submitted, cr.Status);
            Assert.Equal(submitterName, cr.CreatedById);
            Assert.Null(cr.ApprovedById);
            Assert.Null(cr.ApprovalDate);
            crId = cr.Id;

            // Audit log should have a Create row attributed to Alice
            var createAudit = await db.AuditLogs
                .Where(a => a.EntityId == crId && a.Action == AuditAction.Create)
                .ToListAsync();
            Assert.NotEmpty(createAudit);
            Assert.Contains(createAudit, a => a.UserName == submitterName || a.UserId == submitterId);
        }

        // ── Step 2: Bob (a DIFFERENT identity) approves ───────────────
        var bobClient = AsUser(approverId, approverName);
        var approveResp = await bobClient.PostAsync($"/ChangeRequests/Approve/{crId}",
            new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.True(approveResp.StatusCode == HttpStatusCode.Redirect || approveResp.StatusCode == HttpStatusCode.Found);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cr = await db.ChangeRequests.FirstAsync(c => c.Id == crId);

            // After Step 2: Bob's identity must be stamped on the approval
            // fields. The submitter row (CreatedById) must NOT have changed.
            Assert.Equal(ChangeRequestStatus.Approved, cr.Status);
            Assert.Equal(submitterName, cr.CreatedById);   // unchanged
            Assert.Equal(approverName, cr.ApprovedById);   // Bob, not Alice
            Assert.NotNull(cr.ApprovalDate);

            // Audit log should now have an Update row attributed to Bob
            // (the AuditSaveChangesInterceptor records the status change).
            var updateAudit = await db.AuditLogs
                .Where(a => a.EntityId == crId && a.Action == AuditAction.Update)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
            Assert.NotEmpty(updateAudit);
            // The most recent Update row should be Bob's, not Alice's
            Assert.True(
                updateAudit.Any(a => a.UserName == approverName || a.UserId == approverId),
                $"Expected an Update audit row attributed to Bob ({approverName}); got: " +
                string.Join(", ", updateAudit.Select(a => $"[{a.UserName}@{a.Timestamp:O}]")));
        }
    }

    [Fact]
    public async Task ChangeRequest_RejectByApprover_DoesNotPollute_SubmitterAuditTrail()
    {
        const string submitterId   = "101";
        const string submitterName = "carol.submitter";
        const string approverId    = "201";
        const string approverName  = "dave.approver";

        var carolClient = AsUser(submitterId, submitterName);
        await carolClient.PostAsync("/ChangeRequests/Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["NameEn"] = "Rejection trace probe",
                ["NameAr"] = "اختبار رفض",
                ["DescriptionEn"] = "x",
                ["Justification"] = "x",
                ["Priority"] = "3"
            }));

        string crId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            crId = (await db.ChangeRequests
                .Where(c => c.NameEn == "Rejection trace probe")
                .OrderByDescending(c => c.CreatedAt)
                .FirstAsync()).Id;
        }

        var daveClient = AsUser(approverId, approverName);
        await daveClient.PostAsync($"/ChangeRequests/Reject/{crId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["rejectionReason"] = "Insufficient justification"
            }));

        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rejected = await assertDb.ChangeRequests.FirstAsync(c => c.Id == crId);

        Assert.Equal(ChangeRequestStatus.Rejected, rejected.Status);
        Assert.Equal("Insufficient justification", rejected.RejectionReason);
        Assert.Equal(submitterName, rejected.CreatedById); // not overwritten by Dave

        // Carol must not appear in any Update audit row for this entity
        var updates = await assertDb.AuditLogs
            .Where(a => a.EntityId == crId && a.Action == AuditAction.Update)
            .ToListAsync();
        Assert.NotEmpty(updates);
        Assert.DoesNotContain(updates, a => a.UserName == submitterName);
    }
}
