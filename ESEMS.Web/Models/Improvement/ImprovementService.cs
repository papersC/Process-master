using ESEMS.Web.Models.Services;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Junction table for many-to-many relationship between ImprovementInitiative and Service
/// </summary>
public class ImprovementService
{
    public string ImprovementId { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }

    // Navigation
    public ImprovementInitiative? Improvement { get; set; }
    public Service? Service { get; set; }
}
