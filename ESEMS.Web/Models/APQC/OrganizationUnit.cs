using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Models.APQC;

/// <summary>
/// Organization unit — the single org table, mapped to [organization_units].
/// Merged from the former business OrganizationUnit (GUID) and the auth-side
/// CustomOrganizationUnit (int). Key is now int (column unit_id) to match the
/// client's table; overlapping columns use the auth column names/types
/// (unit_name, unit_name_ar, unit_type as string, parent_unit as int, the
/// created_by/created_date audit shape). Business-only fields (Code, Level,
/// DisplayOrder, descriptions, IsActive/IsDeleted) are kept as extra columns.
/// </summary>
[Table("organization_units")]
public class OrganizationUnit
{
    [Column("unit_id")]
    public int Id { get; set; }

    [Column("unit_name")]
    public string NameEn { get; set; } = string.Empty;

    [Column("unit_name_ar")]
    public string NameAr { get; set; } = string.Empty;

    public string? DescriptionEn { get; set; }
    public string? DescriptionAr { get; set; }

    /// <summary>Organization unit code (business extra — no auth equivalent).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Parent organization unit id (self-referencing hierarchy).</summary>
    [Column("parent_unit")]
    public int? ParentId { get; set; }

    /// <summary>Level in the hierarchy (0=Sector … 4=SubFunction).</summary>
    public int Level { get; set; } = 1;

    /// <summary>Strongly-typed view over <see cref="Level"/> — not mapped.</summary>
    [NotMapped]
    public OrganizationLevel OrganizationLevel
    {
        get => (OrganizationLevel)Level;
        set => Level = (int)value;
    }

    public int DisplayOrder { get; set; }

    /// <summary>
    /// Classification (Sector / Department / Section / …). Stored as free text
    /// in column unit_type to match the client table. Was an enum on the old
    /// business entity.
    /// </summary>
    [Column("unit_type")]
    public string? UnitType { get; set; }

    public bool IsActive { get; set; } = true;

    public string? HeadUserId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    // Audit — mapped to the client table's column names/types.
    [Column("created_date")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("update_date")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [Column("created_by")]
    public int? CreatedById { get; set; }
    [Column("update_by")]
    public int? UpdatedById { get; set; }
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Localized name/description (formerly inherited from BilingualEntity).
    [NotMapped]
    public string Name => GetLocalizedName();
    [NotMapped]
    public string? Description => GetLocalizedDescription();

    public string GetLocalizedName()
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        if (culture.StartsWith("ar"))
            return !string.IsNullOrEmpty(NameAr) ? NameAr : NameEn;
        return !string.IsNullOrEmpty(NameEn) ? NameEn : NameAr;
    }

    public string? GetLocalizedDescription()
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        if (culture.StartsWith("ar"))
            return !string.IsNullOrEmpty(DescriptionAr) ? DescriptionAr : DescriptionEn;
        return !string.IsNullOrEmpty(DescriptionEn) ? DescriptionEn : DescriptionAr;
    }

    // Navigation properties
    public OrganizationUnit? Parent { get; set; }
    [NotMapped]
    public User? HeadUser { get; set; }
    public ICollection<OrganizationUnit> Children { get; set; } = new List<OrganizationUnit>();
    public ICollection<Process> OwnedProcesses { get; set; } = new List<Process>();
    public ICollection<Activity> OwnedActivities { get; set; } = new List<Activity>();
    public ICollection<ProcessTask> OwnedTasks { get; set; } = new List<ProcessTask>();
    public ICollection<OrganizationUnitResponsibility> OwnedResponsibilities { get; set; } = new List<OrganizationUnitResponsibility>();

    /// <summary>Full path e.g. "Department &gt; Section &gt; Function".</summary>
    public string GetFullPath()
    {
        var path = new List<string> { Name };
        var current = Parent;
        while (current != null)
        {
            path.Insert(0, current.Name);
            current = current.Parent;
        }
        return string.Join(" > ", path);
    }

    public string GetLevelName() => Level switch
    {
        0 => "Sector",
        1 => "Department",
        2 => "Section",
        3 => "Function",
        4 => "SubFunction",
        _ => "Unit"
    };

    public string GetLevelNameAr() => Level switch
    {
        0 => "قطاع",
        1 => "إدارة",
        2 => "قسم",
        3 => "وحدة",
        4 => "وحدة فرعية",
        _ => "وحدة"
    };

    /// <summary>Human-readable classification — the free-text UnitType when set,
    /// otherwise the level name.</summary>
    public string GetUnitTypeName() =>
        string.IsNullOrWhiteSpace(UnitType) ? GetLevelName() : UnitType!;

    public string GetUnitTypeNameAr() =>
        string.IsNullOrWhiteSpace(UnitType) ? GetLevelNameAr() : UnitType!;
}
