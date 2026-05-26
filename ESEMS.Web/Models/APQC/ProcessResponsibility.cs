namespace ESEMS.Web.Models.APQC;

/// <summary>
/// M2M join: a Process fulfills one or more chartered OrganizationUnit
/// responsibilities (mandates). Mirrors the existing ProcessService /
/// ProcessStrategicObjective shape.
/// </summary>
public class ProcessResponsibility
{
    public string ProcessId { get; set; } = string.Empty;
    public string ResponsibilityId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
    public bool IsActive { get; set; } = true;

    public Process? Process { get; set; }
    public OrganizationUnitResponsibility? Responsibility { get; set; }
}
