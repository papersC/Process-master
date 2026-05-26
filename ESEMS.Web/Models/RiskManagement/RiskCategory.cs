using ESEMS.Web.Models.Common;

namespace ESEMS.Web.Models.RiskManagement;

/// <summary>
/// Represents a risk category (ISO 31000:2018)
/// </summary>
public class RiskCategory : AuditableBilingualEntity
{
    /// <summary>
    /// Category code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Parent category ID (for hierarchical categorization)
    /// </summary>
    public string? ParentCategoryId { get; set; }

    /// <summary>
    /// Default review frequency (in days)
    /// </summary>
    public int? DefaultReviewFrequencyDays { get; set; }

    // Navigation properties
    public RiskCategory? ParentCategory { get; set; }
    public ICollection<RiskCategory> SubCategories { get; set; } = new List<RiskCategory>();
    public ICollection<EnterpriseRisk> Risks { get; set; } = new List<EnterpriseRisk>();
}

