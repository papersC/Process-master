namespace ESEMS.Web.Models.Common;

/// <summary>
/// Generic key/value application setting row. Backs every tab of the
/// Settings Hub (General, Email &amp; Alerts, Integrations, Data Import
/// preferences, etc.) so we don't need one table per feature. The
/// <see cref="Category"/> column lets the UI group rows in the General
/// tab, while specific tabs query by well-known keys.
/// </summary>
public class AppSetting
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Stable unique key (e.g. "Smtp.Host", "Ai.Enabled", "Alerts.DigestFrequency").
    /// Dotted prefixes act as a loose namespace per tab.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Value serialized as a string. bool = "true"/"false".</summary>
    public string? Value { get; set; }

    /// <summary>Category label — shown in the General tab as a section header.</summary>
    public string Category { get; set; } = "General";

    /// <summary>bool, int, decimal, string, json</summary>
    public string DataType { get; set; } = "string";

    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }

    /// <summary>If true, hide from the General tab (because a dedicated tab owns it).</summary>
    public bool Hidden { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
