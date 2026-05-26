using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ESEMS.Web.Models.Common;

namespace ESEMS.Web.Models.APQC;

/// <summary>
/// Tracks version history of BPMN diagrams for processes
/// </summary>
public class ProcessBpmnVersion
{
    /// <summary>
    /// Unique identifier for the version record
    /// </summary>
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Reference to the parent process
    /// </summary>
    [Required]
    public string ProcessId { get; set; } = string.Empty;

    /// <summary>
    /// Version number (auto-incremented per process)
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// The BPMN 2.0 XML content at this version
    /// </summary>
    [Required]
    public string BpmnXml { get; set; } = string.Empty;

    /// <summary>
    /// Description of changes made in this version
    /// </summary>
    [MaxLength(500)]
    public string? ChangeDescription { get; set; }

    /// <summary>
    /// User ID who created this version
    /// </summary>
    public string? CreatedById { get; set; }

    /// <summary>
    /// Username who created this version (denormalized for display)
    /// </summary>
    [MaxLength(256)]
    public string? CreatedByName { get; set; }

    /// <summary>
    /// Timestamp when this version was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this is the current active version
    /// </summary>
    public bool IsCurrent { get; set; }

    /// <summary>
    /// Size of the BPMN XML in bytes (for display purposes)
    /// </summary>
    public int XmlSizeBytes { get; set; }

    // Navigation properties
    [ForeignKey(nameof(ProcessId))]
    public Process? Process { get; set; }

    [NotMapped]
    public User? CreatedBy { get; set; }

    /// <summary>
    /// Get a summary of the version for display
    /// </summary>
    public string GetVersionSummary()
    {
        return $"v{VersionNumber} - {CreatedAt:yyyy-MM-dd HH:mm}";
    }

    /// <summary>
    /// Get formatted file size
    /// </summary>
    public string GetFormattedSize()
    {
        if (XmlSizeBytes < 1024)
            return $"{XmlSizeBytes} B";
        if (XmlSizeBytes < 1024 * 1024)
            return $"{XmlSizeBytes / 1024.0:F1} KB";
        return $"{XmlSizeBytes / (1024.0 * 1024.0):F1} MB";
    }
}

