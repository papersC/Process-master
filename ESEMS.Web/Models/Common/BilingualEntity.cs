using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace ESEMS.Web.Models.Common;

/// <summary>
/// Base class for entities with bilingual (Arabic/English) support
/// </summary>
public abstract class BilingualEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // English fields
    [Required]
    [MinLength(3)]
    public string NameEn { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }

    // Arabic fields
    [Required]
    [MinLength(3)]
    public string NameAr { get; set; } = string.Empty;
    public string? DescriptionAr { get; set; }

    /// <summary>
    /// Gets the localized name based on current culture
    /// </summary>
    public string Name => GetLocalizedName();

    /// <summary>
    /// Gets the localized description based on current culture
    /// </summary>
    public string? Description => GetLocalizedDescription();

    /// <summary>
    /// Returns name in the current UI culture
    /// </summary>
    public string GetLocalizedName()
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        if (culture.StartsWith("ar"))
        {
            return !string.IsNullOrEmpty(NameAr) ? NameAr : NameEn;
        }
        return !string.IsNullOrEmpty(NameEn) ? NameEn : NameAr;
    }

    /// <summary>
    /// Returns description in the current UI culture
    /// </summary>
    public string? GetLocalizedDescription()
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        if (culture.StartsWith("ar"))
        {
            return !string.IsNullOrEmpty(DescriptionAr) ? DescriptionAr : DescriptionEn;
        }
        return !string.IsNullOrEmpty(DescriptionEn) ? DescriptionEn : DescriptionAr;
    }
}

/// <summary>
/// Base class for auditable bilingual entities
/// </summary>
public abstract class AuditableBilingualEntity : BilingualEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}

/// <summary>
/// Base class for entities that support time and cost measurement
/// </summary>
public abstract class MeasurableEntity : AuditableBilingualEntity
{
    /// <summary>
    /// Estimated duration value
    /// </summary>
    public decimal? EstimatedDuration { get; set; }

    /// <summary>
    /// Time unit for the duration
    /// </summary>
    public Enums.TimeUnit? DurationUnit { get; set; }

    /// <summary>
    /// Estimated cost
    /// </summary>
    public decimal? EstimatedCost { get; set; }

    /// <summary>
    /// Actual duration value
    /// </summary>
    public decimal? ActualDuration { get; set; }

    /// <summary>
    /// Time unit for actual duration
    /// </summary>
    public Enums.TimeUnit? ActualDurationUnit { get; set; }

    /// <summary>
    /// Actual cost
    /// </summary>
    public decimal? ActualCost { get; set; }

    /// <summary>
    /// Whether this is a manual or automated process/activity
    /// </summary>
    public bool IsAutomated { get; set; } = false;

    /// <summary>
    /// Converts duration to minutes for aggregation
    /// </summary>
    public decimal? GetDurationInMinutes()
    {
        if (!EstimatedDuration.HasValue || !DurationUnit.HasValue)
            return null;

        return DurationUnit.Value switch
        {
            Enums.TimeUnit.Minutes => EstimatedDuration.Value,
            Enums.TimeUnit.Hours => EstimatedDuration.Value * 60,
            Enums.TimeUnit.Days => EstimatedDuration.Value * 60 * 8, // 8-hour workday
            _ => EstimatedDuration.Value
        };
    }
}

