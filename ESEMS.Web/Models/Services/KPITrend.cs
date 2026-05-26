namespace ESEMS.Web.Models.Services;

/// <summary>
/// Historical KPI trend tracking (Draft7 - MBRHE Excellence Journey).
/// Tracks happiness, digital adoption, and other KPIs over time.
/// </summary>
public class KPITrend
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>KPI category (e.g., "Customer Happiness", "Employee Happiness", "Digital Adoption")</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>KPI name (e.g., "Customer Happiness Index", "Employee Happiness Index")</summary>
    public string KPIName { get; set; } = string.Empty;

    /// <summary>Year of measurement</summary>
    public int Year { get; set; }

    /// <summary>KPI value (percentage or absolute)</summary>
    public decimal Value { get; set; }

    /// <summary>Target value for the year</summary>
    public decimal? TargetValue { get; set; }

    /// <summary>Unit of measure (%, AED, count, etc.)</summary>
    public string Unit { get; set; } = "%";

    /// <summary>Additional notes</summary>
    public string? Notes { get; set; }
}

