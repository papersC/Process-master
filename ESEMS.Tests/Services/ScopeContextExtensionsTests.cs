using ESEMS.Web.Models.Common;
using ESEMS.Web.Services.Common;

namespace ESEMS.Tests.Services;

/// <summary>
/// Locks the contract of <see cref="ScopeContextExtensions.CanAccess"/> —
/// the per-record IDOR guard used by EnterpriseRisksController.Details/Edit/
/// Delete (and to be applied to the other 5 scoped controllers; see TODO
/// breadcrumbs in source).
///
/// Senior-tester reasoning: the auth-policy attribute (<c>[Authorize(Policy =
/// "Risk.View")]</c>) only checks "this user has the View claim". It does NOT
/// check "this user can see THIS record". Without per-record scope, a
/// Risk.View user with ScopeLevel=OwningUnit can navigate to /Risk/Details/{any-id}
/// and read records outside their scope. This test pins the policy that
/// closes that gap.
///
/// Unit IDs are int since the org-unit merge (was GUID string).
/// </summary>
public class ScopeContextExtensionsTests
{
    private sealed record Org(int? OrganizationUnitId) : IOrganizationScoped;
    private sealed record Owned(int? OwningUnitId) : IOwnedByUnit;
    private sealed record Assigned(int? AssignedToUnitId) : IAssignedToUnit;

    [Fact]
    public void Unscoped_User_CanAccess_Anything()
    {
        var scope = ScopeContext.Unscoped;
        Assert.True(scope.CanAccess(new Org(1)));
        Assert.True(scope.CanAccess(new Org(null)));
        Assert.True(scope.CanAccess(new Owned(2)));
        Assert.True(scope.CanAccess(new Assigned(3)));
    }

    [Fact]
    public void Scoped_User_CanAccess_OwnUnit_Records()
    {
        var scope = new ScopeContext
        {
            ScopeLevel = "OwningUnit",
            VisibleUnitIds = new HashSet<int> { 1, 2 }
        };
        Assert.True(scope.CanAccess(new Org(1)));
        Assert.True(scope.CanAccess(new Org(2)));
    }

    [Fact]
    public void Scoped_User_Cannot_Access_OutOfScope_Records()
    {
        var scope = new ScopeContext
        {
            ScopeLevel = "OwningUnit",
            VisibleUnitIds = new HashSet<int> { 1 }
        };
        // The IDOR scenario: user with ScopeLevel=OwningUnit + visible unit 1
        // tries to navigate to a record whose OrganizationUnitId is unit 3.
        Assert.False(scope.CanAccess(new Org(3)));
        Assert.False(scope.CanAccess(new Owned(3)));
        Assert.False(scope.CanAccess(new Assigned(3)));
    }

    [Fact]
    public void Records_With_Null_Unit_Are_Visible_To_Everyone()
    {
        // Orphan records (no OrganizationUnitId set) intentionally fall through
        // to "visible" so a partial-data record isn't accidentally hidden from
        // a scoped user.
        var scope = new ScopeContext
        {
            ScopeLevel = "OwningUnit",
            VisibleUnitIds = new HashSet<int> { 1 }
        };
        Assert.True(scope.CanAccess(new Org(null)));
        Assert.True(scope.CanAccess(new Owned(null)));
        Assert.True(scope.CanAccess(new Assigned(null)));
    }
}
