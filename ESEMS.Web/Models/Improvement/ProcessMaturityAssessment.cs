namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Process Maturity Assessment based on 7 APQC pillars (Draft7 - MBRHE Process Excellence).
/// Tracks maturity scores across years for benchmarking.
/// </summary>
public class ProcessMaturityAssessment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Assessment year (e.g., 2020, 2023)</summary>
    public int AssessmentYear { get; set; }

    /// <summary>Strategic Alignment score (0-5)</summary>
    public decimal StrategicAlignment { get; set; }

    /// <summary>Governance score (0-5)</summary>
    public decimal Governance { get; set; }

    /// <summary>Process Models score (0-5)</summary>
    public decimal ProcessModels { get; set; }

    /// <summary>Change Management score (0-5)</summary>
    public decimal ChangeManagement { get; set; }

    /// <summary>Process Performance score (0-5)</summary>
    public decimal ProcessPerformance { get; set; }

    /// <summary>Process Improvement score (0-5)</summary>
    public decimal ProcessImprovement { get; set; }

    /// <summary>Tools & Technology score (0-5)</summary>
    public decimal ToolsAndTechnology { get; set; }

    /// <summary>Overall maturity score (0-5)</summary>
    public decimal OverallScore { get; set; }

    /// <summary>Assessment date</summary>
    public DateTime AssessmentDate { get; set; } = DateTime.UtcNow;

    /// <summary>Additional notes</summary>
    public string? Notes { get; set; }
}

