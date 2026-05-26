using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESEMS.Web.Models.DocumentManagement;

/// <summary>
/// A file uploaded by a user into their personal "My Space" library.
/// Files are stored on disk under wwwroot/uploads/myspace/{UserId}/{FileName}.
/// The record is the source of truth for every document the user can attach
/// to other entities (processes, improvements, etc.).
/// </summary>
public class UserDocument
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Owning user (FK to CustomUser.UserId). Only the owner can list,
    /// download, or delete their documents.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Server-side disk filename (GUID + extension).
    /// </summary>
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Original filename as uploaded by the user.
    /// </summary>
    [MaxLength(500)]
    public string OriginalName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Optional free-form tags (comma-separated).
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// Optional user-chosen category (e.g., Policy, Procedure, Evidence).
    /// </summary>
    [MaxLength(100)]
    public string Category { get; set; } = "General";

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }

    // Navigation
    [ForeignKey(nameof(UserId))]
    public CustomUser? User { get; set; }
}
