using System.Net;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.Improvement;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ESEMS.Tests.Integration;

/// <summary>
/// Regression tests for commit e59a3a5 — IsActionable() guard on
/// ChangeRequestsController.Approve / Reject. The pre-fix bug let an
/// approver re-approve an already-Approved request (overwriting
/// ApprovedById/ApprovalDate with their own credentials) or reject an
/// Implemented request (flipping the status backwards out of a terminal
/// state). The fix short-circuits with a TempData["Error"] redirect when
/// the current status isn't Submitted or UnderReview.
///
/// These tests would FAIL if the guard is removed in a future refactor.
/// </summary>
public class ChangeRequestStateGuardTests : IClassFixture<EsemsTestFactory>
{
    private readonly EsemsTestFactory _factory;
    public ChangeRequestStateGuardTests(EsemsTestFactory factory) { _factory = factory; }

    private async Task<string> SeedAsync(ChangeRequestStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cr = new ChangeRequest
        {
            Id = Guid.NewGuid().ToString(),
            Code = "CR-TEST-" + Guid.NewGuid().ToString("N")[..6],
            NameEn = "Regression test CR",
            NameAr = "اختبار",
            DescriptionEn = "x",
            Justification = "x",
            Priority = 3,
            Status = status,
            ApprovedById = "previous.approver",
            ApprovalDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            CreatedById = "1",
            IsDeleted = false
        };
        db.ChangeRequests.Add(cr);
        await db.SaveChangesAsync();
        return cr.Id;
    }

    private async Task<ChangeRequest> ReadAsync(string id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.ChangeRequests.FirstAsync(c => c.Id == id);
    }

    private HttpClient ApproverClient()
    {
        // ADMIN role + wildcard permission so both Approve policy and
        // ScopingService.CanAccess pass; bug under test is independent of
        // those gates.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", "42");
        client.DefaultRequestHeaders.Add("X-Test-Name", "regression.approver");
        client.DefaultRequestHeaders.Add("X-Test-Role", "ADMIN");
        client.DefaultRequestHeaders.Add("X-Test-Perms", "*.*");
        return client;
    }

    private static FormUrlEncodedContent EmptyForm() =>
        new(new Dictionary<string, string>
        {
            // The default antiforgery handler doesn't run under TestAuthHandler,
            // but we send an empty body anyway so the model binder is happy.
        });

    [Fact]
    public async Task Approve_OnAlreadyApprovedRecord_DoesNotOverwriteAuditFields()
    {
        var id = await SeedAsync(ChangeRequestStatus.Approved);

        var client = ApproverClient();
        var resp = await client.PostAsync($"/ChangeRequests/Approve/{id}", EmptyForm());

        // Controller should redirect back to Details with an error TempData;
        // it must NOT have rewritten ApprovedById / ApprovalDate.
        Assert.True(resp.StatusCode == HttpStatusCode.Redirect || resp.StatusCode == HttpStatusCode.Found);

        var cr = await ReadAsync(id);
        Assert.Equal(ChangeRequestStatus.Approved, cr.Status);
        Assert.Equal("previous.approver", cr.ApprovedById);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), cr.ApprovalDate);
    }

    [Fact]
    public async Task Reject_OnImplementedRecord_DoesNotFlipStatusBackwards()
    {
        var id = await SeedAsync(ChangeRequestStatus.Implemented);

        var client = ApproverClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["rejectionReason"] = "trying to reject a closed CR"
        });
        var resp = await client.PostAsync($"/ChangeRequests/Reject/{id}", form);

        Assert.True(resp.StatusCode == HttpStatusCode.Redirect || resp.StatusCode == HttpStatusCode.Found);

        var cr = await ReadAsync(id);
        // Status must not have flipped to Rejected; rejection reason must not
        // have been written.
        Assert.Equal(ChangeRequestStatus.Implemented, cr.Status);
        Assert.Null(cr.RejectionReason);
    }

    [Fact]
    public async Task Approve_OnSubmittedRecord_StillWorks_HappyPath()
    {
        // Make sure the guard didn't accidentally block the legitimate path.
        var id = await SeedAsync(ChangeRequestStatus.Submitted);

        var client = ApproverClient();
        var resp = await client.PostAsync($"/ChangeRequests/Approve/{id}", EmptyForm());
        var body = await resp.Content.ReadAsStringAsync();

        Assert.True(
            resp.StatusCode == HttpStatusCode.Redirect || resp.StatusCode == HttpStatusCode.Found,
            $"Expected redirect, got {(int)resp.StatusCode} {resp.StatusCode}. Body: {body[..Math.Min(500, body.Length)]}");

        var cr = await ReadAsync(id);
        Assert.Equal(ChangeRequestStatus.Approved, cr.Status);
        Assert.Equal("regression.approver", cr.ApprovedById);
    }
}
