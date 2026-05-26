using ESEMS.Web.Models.APQC;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Junction table for many-to-many relationship between ImprovementInitiative and Process
/// </summary>
public class ImprovementProcess
{
    public string ImprovementId { get; set; } = string.Empty;
    public string ProcessId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }

    // Navigation
    public ImprovementInitiative? Improvement { get; set; }
    public Process? Process { get; set; }
}
