using ESEMS.Web.Models.Common;

namespace ESEMS.Web.Models.Services;

/// <summary>
/// ISO Standard tracking for compliance (Draft7 - 22+ ISO Standards adherence).
/// </summary>
public class ISOStandard : BilingualEntity
{
    /// <summary>ISO standard number (e.g., "ISO 9001:2015")</summary>
    public string StandardNumber { get; set; } = string.Empty;

    /// <summary>Year of the standard version</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Domain/category (e.g., "Quality Management", "Risk Management")</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>Whether MBRHE is certified/compliant</summary>
    public bool IsCompliant { get; set; } = true;

    /// <summary>Compliance level (0-100%)</summary>
    public int CompliancePercentage { get; set; } = 100;

    /// <summary>Last audit date</summary>
    public DateTime? LastAuditDate { get; set; }

    /// <summary>Next audit date</summary>
    public DateTime? NextAuditDate { get; set; }

    /// <summary>Additional notes</summary>
    public string? Notes { get; set; }
}

