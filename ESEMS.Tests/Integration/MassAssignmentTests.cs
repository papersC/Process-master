using System.Net;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.Improvement;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ESEMS.Tests.Integration;

/// <summary>
/// Mass-assignment probe (deferred item from the post-redesign audit).
/// Most controllers take the full domain model (e.g. Create(ChangeRequest cr))
/// — a determined caller could POST extra fields the model didn't expect to
/// be settable. The current controllers defend by EXPLICITLY resetting the
/// server-managed fields after binding (Id, Code, CreatedAt, CreatedById,
/// Status). These tests document that defense and would FAIL if someone
/// later removes the override block, opening a privilege-escalation hole.
/// </summary>
public class MassAssignmentTests : IClassFixture<EsemsTestFactory>
{
    private readonly EsemsTestFactory _factory;
    public MassAssignmentTests(EsemsTestFactory factory) { _factory = factory; }

    private HttpClient EditorClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", "10");
        client.DefaultRequestHeaders.Add("X-Test-Name", "attacker.editor");
        client.DefaultRequestHeaders.Add("X-Test-Role", "ADMIN");
        client.DefaultRequestHeaders.Add("X-Test-Perms", "*.*");
        return client;
    }

    [Fact]
    public async Task ChangeRequestCreate_IgnoresForgedStatusAndAuditFields()
    {
        // Submit a Create form WITH forged audit + status fields set. The
        // controller's explicit-override block at lines 127-133 of
        // ChangeRequestsController is supposed to clobber every one of these.
        var forgedFutureDate = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var formFields = new Dictionary<string, string>
        {
            // Legitimate fields
            ["NameEn"] = "Mass-assignment probe",
            ["NameAr"] = "اختبار",
            ["DescriptionEn"] = "probe",
            ["Justification"] = "probe",
            ["Priority"] = "3",
            // Forged fields — these should NOT survive
            ["Id"] = "forged-id-12345",
            ["Code"] = "CR-FORGED-9999",
            ["Status"] = ((int)ChangeRequestStatus.Approved).ToString(),
            ["ApprovedById"] = "victim.user",
            ["ApprovalDate"] = forgedFutureDate.ToString("o"),
            ["CreatedById"] = "victim.user",
            ["IsDeleted"] = "true"
        };

        var resp = await EditorClient().PostAsync("/ChangeRequests/Create", new FormUrlEncodedContent(formFields));

        // Successful create redirects to Index or Details
        Assert.True(
            resp.StatusCode == HttpStatusCode.Redirect || resp.StatusCode == HttpStatusCode.Found,
            $"Expected redirect after Create, got {(int)resp.StatusCode} {resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.ChangeRequests
            .Where(c => c.NameEn == "Mass-assignment probe")
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();
        Assert.NotNull(saved);

        // Forged Id must not have survived — controller assigns a fresh GUID
        Assert.NotEqual("forged-id-12345", saved!.Id);

        // Forged Code must not have survived — controller calls
        // GenerateNextChangeRequestCodeAsync()
        Assert.NotEqual("CR-FORGED-9999", saved.Code);
        Assert.StartsWith("CR-", saved.Code);

        // Forged Status must not have survived — ALWAYS reset to Submitted
        Assert.Equal(ChangeRequestStatus.Submitted, saved.Status);

        // Forged CreatedById must not have survived — set from User.Identity.Name
        Assert.NotEqual("victim.user", saved.CreatedById);

        // The controller does NOT explicitly reset ApprovedById / ApprovalDate
        // / IsDeleted on Create. They MIGHT survive — document the current
        // behavior so a future tightening fix can update this test.
        // (Fail-loud if a caller manages to force IsDeleted=true at create.)
        Assert.False(saved.IsDeleted, "A newly created ChangeRequest should not be marked deleted");
    }

    [Fact]
    public async Task ChangeRequestCreate_StampsCreatedByFromAuthenticatedUser()
    {
        // Sanity: even with no forged CreatedById, the field gets stamped from
        // User.Identity.Name. Catches a regression where someone removes the
        // override and binds the form value verbatim.
        var fields = new Dictionary<string, string>
        {
            ["NameEn"] = "stamp-probe",
            ["NameAr"] = "بصمة",
            ["DescriptionEn"] = "x",
            ["Justification"] = "x",
            ["Priority"] = "3"
        };
        var resp = await EditorClient().PostAsync("/ChangeRequests/Create", new FormUrlEncodedContent(fields));
        Assert.True(resp.StatusCode == HttpStatusCode.Redirect || resp.StatusCode == HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.ChangeRequests
            .Where(c => c.NameEn == "stamp-probe")
            .OrderByDescending(c => c.CreatedAt)
            .FirstAsync();

        Assert.False(string.IsNullOrEmpty(saved.CreatedById),
            "CreatedById should be auto-stamped from the authenticated user");
        Assert.Contains("attacker.editor", saved.CreatedById!);
    }
}
