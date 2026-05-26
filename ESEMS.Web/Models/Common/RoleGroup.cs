namespace ESEMS.Web.Models.Common;

/// <summary>
/// Named bundle of permissions / responsibilities, with an optional
/// scope (All / OwningUnit / Process). Inspired by PManagement's
/// RoleGroups pattern but adapted for ESEMS entity shapes.
/// </summary>
public class RoleGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;

    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }

    /// <summary>All / OwningUnit / Process — constrains what rows members can see.</summary>
    public string ScopeLevel { get; set; } = "All";

    /// <summary>
    /// Comma-separated list of permission keys granted to the group
    /// (e.g. "Improvement.Read,Improvement.Edit,Process.Read").
    /// Kept flat on purpose — the UI edits it as a tag cloud.
    /// </summary>
    public string? Permissions { get; set; }

    /// <summary>
    /// Lucide icon name used for the card header (user-check, shield, rocket, etc.)
    /// </summary>
    public string Icon { get; set; } = "users";

    /// <summary>Hex color for the card header accent (e.g. #10b981)</summary>
    public string Color { get; set; } = "#005B99";

    public bool IsActive { get; set; } = true;
    public int MemberCount { get; set; }

    /// <summary>
    /// System-seeded role groups (Quality Officer, Process Owner, etc.) cannot be
    /// deleted via the UI and show a lock badge. Protects critical roles from
    /// being accidentally removed while still allowing admins to edit their
    /// permissions.
    /// </summary>
    public bool IsSystemRole { get; set; }

    /// <summary>
    /// Short kebab-case identifier used as a stable alias for code references.
    /// Auto-generated from NameEn unless the group is system-seeded.
    /// </summary>
    public string? Code { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
