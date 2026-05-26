using ESEMS.Web.Models.APQC;

namespace ESEMS.Web.Models.Services;

/// <summary>
/// M2M join: a Service fulfills one or more chartered OrganizationUnit
/// responsibilities (mandates). Mirrors ServiceStrategicObjective.
/// </summary>
public class ServiceResponsibility
{
    public string ServiceId { get; set; } = string.Empty;
    public string ResponsibilityId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
    public bool IsActive { get; set; } = true;

    public Service? Service { get; set; }
    public OrganizationUnitResponsibility? Responsibility { get; set; }
}
