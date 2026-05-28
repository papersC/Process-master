using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESEMS.Web.Models.APQC;

/// <summary>
/// A BPMN drawing imported from the on-disk library (output2/) that could
/// not be fuzzy-matched to an existing L3 Process at import time. The XML
/// is kept here so a user can later either:
///   - link it manually to a Process via <see cref="LinkedProcessId"/>, or
///   - create a new Process and adopt this drawing as its initial BPMN.
///
/// This is the "parking lot" for the import-all workflow: the user runs
/// the bulk importer once; matched files land on their Process directly,
/// unmatched files land here so nothing is lost.
/// </summary>
public class OrphanBpmnDrawing
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Original filename, e.g. "011_استثمار المشروع.bpmn".</summary>
    [Required, MaxLength(512)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>The name parsed out of the filename — everything after the leading "NNN_" prefix, minus the extension.</summary>
    [Required, MaxLength(512)]
    public string DetectedName { get; set; } = string.Empty;

    /// <summary>The numeric prefix from the filename (e.g. "011") — handy for sorting and quick lookup.</summary>
    [MaxLength(16)]
    public string? FilePrefix { get; set; }

    /// <summary>The raw BPMN 2.0 XML content as read from disk.</summary>
    [Required]
    public string BpmnXml { get; set; } = string.Empty;

    /// <summary>UTF-8 byte size of the XML, kept for the listing UI without a re-encode.</summary>
    public int XmlSizeBytes { get; set; }

    /// <summary>
    /// Best fuzzy-match score found at import time (0..1). Null when no
    /// candidate Process was even close. Stored so the user can sort the
    /// orphan list by "almost-matched these" and review the closest calls.
    /// </summary>
    public double? BestMatchScore { get; set; }

    /// <summary>The Process ID the best (rejected) fuzzy match pointed at. Display-only.
    /// No MaxLength — Process.Id is the EF-default <c>nvarchar(450)</c>, and the FK index
    /// requires matching lengths.</summary>
    public string? BestMatchProcessId { get; set; }

    /// <summary>Process name on that best-match candidate (denormalized — Process may be renamed/deleted later).</summary>
    [MaxLength(512)]
    public string? BestMatchProcessName { get; set; }

    /// <summary>
    /// Once the user manually links this orphan to a Process (and pushes
    /// the BPMN onto it via <c>ProcessBpmnVersion</c>), this points at
    /// that Process and the row becomes archival. Default <c>nvarchar(450)</c>
    /// matches the FK target column.
    /// </summary>
    public string? LinkedProcessId { get; set; }

    /// <summary>UTC timestamp when this orphan was linked to a Process (and effectively retired).</summary>
    public DateTime? LinkedAt { get; set; }

    /// <summary>User who performed the manual linkage.</summary>
    [MaxLength(256)]
    public string? LinkedByName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(LinkedProcessId))]
    public Process? LinkedProcess { get; set; }
}
