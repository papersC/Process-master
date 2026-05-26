using ESEMS.Web.Models.Common;

namespace ESEMS.Web.Models.AssetManagement;

/// <summary>
/// Represents an asset category (ISO 55001:2014)
/// </summary>
public class AssetCategory : AuditableBilingualEntity
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
    /// Default depreciation rate (percentage per year)
    /// </summary>
    public decimal? DefaultDepreciationRate { get; set; }

    /// <summary>
    /// Default useful life (in years)
    /// </summary>
    public int? DefaultUsefulLifeYears { get; set; }

    /// <summary>
    /// Default maintenance interval (in days)
    /// </summary>
    public int? DefaultMaintenanceIntervalDays { get; set; }

    // Navigation properties
    public AssetCategory? ParentCategory { get; set; }
    public ICollection<AssetCategory> SubCategories { get; set; } = new List<AssetCategory>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}

