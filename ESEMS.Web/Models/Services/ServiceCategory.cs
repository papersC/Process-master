using ESEMS.Web.Models.Common;

namespace ESEMS.Web.Models.Services;

/// <summary>
/// Business classification for a Service (e.g. "Grant Services",
/// "Loan Services", "Maintenance"). Promoted from the free-text
/// Service.CategoryEn/CategoryAr pair so dashboards can roll up by category
/// without typo/case-variant drift, and so EN/AR pairs stay in sync.
/// Flat list — Services don't need a hierarchy like AssetCategory.
/// </summary>
public class ServiceCategory : AuditableBilingualEntity
{
    /// <summary>Short code, e.g. "SC-001", "GRANT", "LOAN".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display order for sorting in dropdowns.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Whether this category is selectable. Soft-deleted via IsDeleted.</summary>
    public bool IsActive { get; set; } = true;

    // Navigation — services classified under this category
    public ICollection<Service> Services { get; set; } = new List<Service>();
}
