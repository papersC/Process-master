namespace ESEMS.Web.Models.Common;

/// <summary>
/// Junction entity linking a <see cref="CustomUser"/> to a <see cref="RoleGroup"/>.
/// This is the canonical role/permission assignment in ESEMS. Each assignment
/// grants the user every permission listed in <see cref="RoleGroup.Permissions"/>.
/// A user can be assigned multiple role groups; permissions are OR-ed together.
/// </summary>
public class UserRoleGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>FK to CustomUser.UserId (int)</summary>
    public int UserId { get; set; }

    /// <summary>FK to RoleGroup.Id (string/Guid)</summary>
    public string RoleGroupId { get; set; } = string.Empty;

    /// <summary>Who assigned this group — for auditing.</summary>
    public int? AssignedBy { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public CustomUser? User { get; set; }
    public RoleGroup? RoleGroup { get; set; }
}
