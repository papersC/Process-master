using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ESEMS.Web.Models.APQC;

namespace ESEMS.Web.Models.DocumentManagement;

/// <summary>
/// Join row that links a <see cref="Process"/> to a file in a user's
/// <see cref="UserDocument"/> library, along with per-link metadata
/// (category, type, language).
/// Every process document — whether the user picked it from "My Space" or
/// uploaded it fresh from their computer — is backed by a UserDocument row.
/// </summary>
public class ProcessDocument
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Linked process.
    /// </summary>
    public string ProcessId { get; set; } = string.Empty;

    /// <summary>
    /// Underlying file in the uploader's My Space library.
    /// </summary>
    public string UserDocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Document category classification (e.g., Information Security).
    /// FK to <see cref="DocumentCategory"/>.
    /// </summary>
    [MaxLength(450)]
    public string? DocumentCategoryId { get; set; }

    /// <summary>
    /// Document type (Policy, Procedure, Standard...). FK to <see cref="DocumentType"/>.
    /// </summary>
    [MaxLength(450)]
    public string? DocumentTypeId { get; set; }

    /// <summary>
    /// Document language as it applies to this link (Arabic, English, Bilingual).
    /// </summary>
    [MaxLength(50)]
    public string? DocumentLanguage { get; set; }

    /// <summary>
    /// Display order inside the process's document list.
    /// </summary>
    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(300)]
    public string? CreatedById { get; set; }

    // Navigation
    [ForeignKey(nameof(ProcessId))]
    public Process? Process { get; set; }

    [ForeignKey(nameof(UserDocumentId))]
    public UserDocument? UserDocument { get; set; }

    [ForeignKey(nameof(DocumentCategoryId))]
    public DocumentCategory? DocumentCategory { get; set; }

    [ForeignKey(nameof(DocumentTypeId))]
    public DocumentType? DocumentType { get; set; }
}
