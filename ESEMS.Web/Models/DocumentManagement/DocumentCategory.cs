using ESEMS.Web.Models.Common;

namespace ESEMS.Web.Models.DocumentManagement;

/// <summary>
/// Document classification category (ISO-style).
/// Seeded from the Process Catalog reference sheet (full.xlsx → Sheet17 → "فئة الوثيقة").
/// Examples: Information Security, Business Continuity, Quality Management, Risk Management, Corporate Governance.
/// </summary>
public class DocumentCategory : AuditableBilingualEntity
{
    /// <summary>
    /// Short code used for sorting and API references (e.g. "IS", "BC", "QM").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display order for sorting in dropdowns.
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Whether this category is selectable. Deleted entries are soft-deleted via IsDeleted.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
