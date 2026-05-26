using ESEMS.Web.Models.Common;

namespace ESEMS.Web.Models.DocumentManagement;

/// <summary>
/// Type of the document that describes a process (Policy, Procedure, Standard, Guideline, Plan, etc.).
/// Seeded from the Process Catalog reference sheet (full.xlsx → Sheet17 → "نوع الوثيقة").
/// </summary>
public class DocumentType : AuditableBilingualEntity
{
    /// <summary>
    /// Short code used for sorting and API references (e.g. "POL", "PROC", "STD").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display order for sorting in dropdowns.
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Whether this type is selectable. Deleted entries are soft-deleted via IsDeleted.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
