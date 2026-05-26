namespace ESEMS.Web.Models.Common;

/// <summary>
/// Marker interfaces for data-scoping. Each matches one of the three FK
/// naming patterns used across ESEMS entities to link to an OrganizationUnit.
/// The <see cref="Services.Common.ScopingService"/> uses these interfaces
/// via the <c>QueryableScopeExtensions</c> to filter list queries based on
/// the logged-in user's <c>ScopeLevel</c> claim.
/// </summary>

/// <summary>Entities with an <c>OwningUnitId</c> FK (Process, Service, Improvement, ChangeRequest).</summary>
public interface IOwnedByUnit
{
    int? OwningUnitId { get; }
}

/// <summary>Entities with an <c>AssignedToUnitId</c> FK (Asset, Incident, Problem).</summary>
public interface IAssignedToUnit
{
    int? AssignedToUnitId { get; }
}

/// <summary>Entities with an <c>OrganizationUnitId</c> FK (EnterpriseRisk).</summary>
public interface IOrganizationScoped
{
    int? OrganizationUnitId { get; }
}
