using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ESEMS.Web.Models.APQC;

namespace ESEMS.Web.Models;

/// <summary>
/// Custom User model mapped to existing [user] table
/// </summary>
[Table("user")]
public class CustomUser
{
    [Key]
    [Column("user_id")]
    public int UserId { get; set; }

    [Required]
    [Column("username")]
    [MaxLength(300)]
    public string Username { get; set; } = string.Empty;

    [Column("employee_number")]
    [MaxLength(50)]
    public string? EmployeeNumber { get; set; }

    [Column("email_address")]
    [MaxLength(150)]
    public string? EmailAddress { get; set; }

    [Column("full_name")]
    [MaxLength(200)]
    public string? FullName { get; set; }

    [Column("employee_name")]
    [MaxLength(150)]
    public string? EmployeeName { get; set; }

    [Column("employee_name_ar")]
    [MaxLength(150)]
    public string? EmployeeNameAr { get; set; }

    [Column("job_name")]
    [MaxLength(150)]
    public string? JobName { get; set; }

    [Column("job_name_ar")]
    [MaxLength(150)]
    public string? JobNameAr { get; set; }

    [Column("direct_org_name_en")]
    [MaxLength(150)]
    public string? DirectOrgNameEn { get; set; }

    [Column("direct_org_name_ar")]
    [MaxLength(150)]
    public string? DirectOrgNameAr { get; set; }

    [Column("sector_id")]
    public int? SectorId { get; set; }

    [Column("unit_id")]
    public int? UnitId { get; set; }

    [Column("section_id")]
    public int? SectionId { get; set; }

    [Column("department")]
    [MaxLength(150)]
    public string? Department { get; set; }

    [Column("department_ar")]
    [MaxLength(150)]
    public string? DepartmentAr { get; set; }

    [Column("direct_manager")]
    public int? DirectManager { get; set; }

    [Column("is_department_coordinator")]
    public bool? IsDepartmentCoordinator { get; set; }

    // MaxLength matches the DB column ([user].[password] is nvarchar(256)).
    // Was [MaxLength(50)] before — which caused the Edit form's hidden
    // <input asp-for="Password" /> round-trip to fail model-binding
    // validation, because the stored PBKDF2 base64 hash is 64 chars. The
    // controller silently fell into `return View(user)` (no log, no visible
    // error because the field has no validation span and the summary is
    // ModelOnly), making role-group edits look like they "didn't save."
    [Column("password")]
    [MaxLength(256)]
    public string? Password { get; set; }

    /// <summary>
    /// FU-002: opaque per-user token re-issued on each password change. Emitted
    /// as a "SecurityStamp" claim at login and re-checked by the cookie auth
    /// OnValidatePrincipal event; when it changes, every previously-issued auth
    /// cookie (carrying the old stamp) is rejected — so changing a password
    /// signs out all OTHER sessions. Null is treated as the empty stamp.
    /// </summary>
    [Column("security_stamp")]
    [MaxLength(64)]
    public string? SecurityStamp { get; set; }

    /// <summary>
    /// SQL Server rowversion / EF concurrency token. Stamped by SQL on every
    /// UPDATE; round-tripped through the Edit form as a hidden field so two
    /// admins editing the same user can't silently overwrite each other —
    /// the second save throws DbUpdateConcurrencyException and the controller
    /// returns a "refresh and retry" message.
    /// </summary>
    [Timestamp]
    [Column("row_version")]
    public byte[]? RowVersion { get; set; }

    [Column("points")]
    public int? Points { get; set; }

    [Column("innovator_level")]
    public int? InnovatorLevel { get; set; }

    [Column("has_idea_generator_badge")]
    public bool? HasIdeaGeneratorBadge { get; set; }

    [Column("has_innovator_badge")]
    public bool? HasInnovatorBadge { get; set; }

    [Column("has_visionary_badge")]
    public bool? HasVisionaryBadge { get; set; }

    [Column("has_milestone_achiever_badge")]
    public bool? HasMilestoneAchieverBadge { get; set; }

    [Column("has_impactful_contributor_badge")]
    public bool? HasImpactfulContributorBadge { get; set; }

    // Navigation properties
    [ForeignKey("UnitId")]
    public OrganizationUnit? OrganizationUnit { get; set; }

    [ForeignKey("DirectManager")]
    public CustomUser? Manager { get; set; }

    // Computed properties for compatibility
    [NotMapped]
    public string DisplayName => EmployeeName ?? FullName ?? Username;

    [NotMapped]
    public string DisplayNameAr => EmployeeNameAr ?? EmployeeName ?? Username;

    // FUNC-006: real soft-delete/active flag mapped to [user].[is_active].
    // Deactivated users are preserved (audit/FK history intact) but blocked at login.
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    // Compatibility properties for Identity-based code
    [NotMapped]
    public string Id => UserId.ToString();

    [NotMapped]
    public string Email => EmailAddress ?? string.Empty;

    // System.Text.Json's default camelCase policy turns BOTH `Username` and
    // `UserName` into the same key ("userName" via case-insensitive name
    // collision). .NET 9's stricter JsonTypeInfo build-time check throws
    // InvalidOperationException ("property name collides") whenever
    // [FromBody] tries to register a type that walks here through a nav
    // prop. JsonIgnore on the alias keeps the C# convenience without
    // tripping the serializer.
    [NotMapped]
    [System.Text.Json.Serialization.JsonIgnore]
    public string UserName => Username;

    [NotMapped]
    public string FirstNameEn => EmployeeName ?? string.Empty;

    [NotMapped]
    public string LastNameEn => string.Empty;

    [NotMapped]
    public string? FirstNameAr => EmployeeNameAr;

    [NotMapped]
    public string? LastNameAr => null;

    [NotMapped]
    public string? JobTitleEn => JobName;

    [NotMapped]
    public string? JobTitleAr => JobNameAr;

    [NotMapped]
    public string? OrganizationUnitId => UnitId?.ToString();

    [NotMapped]
    public string? EmployeeId => EmployeeNumber;

    [NotMapped]
    public string FullNameEn => EmployeeName ?? FullName ?? Username;

    [NotMapped]
    public string? FullNameAr => EmployeeNameAr;

    [NotMapped]
    public string PreferredLanguage => "en";

    [NotMapped]
    public string? ProfilePictureUrl => null;

    [NotMapped]
    public DateTime? LastLoginAt => null;

    [NotMapped]
    public DateTime CreatedAt => DateTime.UtcNow;

    [NotMapped]
    public DateTime UpdatedAt => DateTime.UtcNow;

    /// <summary>
    /// Gets the localized full name based on current culture
    /// </summary>
    public string GetLocalizedFullName()
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        if (culture.StartsWith("ar") && !string.IsNullOrEmpty(EmployeeNameAr))
        {
            return EmployeeNameAr;
        }
        return EmployeeName ?? FullName ?? Username;
    }

    /// <summary>
    /// Gets the localized job title based on current culture
    /// </summary>
    public string? GetLocalizedJobTitle()
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        if (culture.StartsWith("ar") && !string.IsNullOrEmpty(JobNameAr))
        {
            return JobNameAr;
        }
        return JobName;
    }
}

