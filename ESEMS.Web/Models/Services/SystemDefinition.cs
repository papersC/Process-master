using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.APQC;

namespace ESEMS.Web.Models.Services;

/// <summary>
/// System definition entity for IT systems used in processes
/// </summary>
public class SystemDefinition : AuditableBilingualEntity
{
    /// <summary>
    /// System code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display order for sorting
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// System vendor/provider
    /// </summary>
    public string? Vendor { get; set; }

    /// <summary>
    /// System software version
    /// </summary>
    public string? SystemVersion { get; set; }

    /// <summary>
    /// System URL (if web-based)
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// System type (e.g., ERP, CRM, BPM, etc.)
    /// </summary>
    public string? SystemType { get; set; }

    /// <summary>
    /// Whether this system is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Owning organizational unit ID
    /// </summary>
    public int? OwningUnitId { get; set; }

    /// <summary>
    /// Support contact information
    /// </summary>
    public string? SupportContact { get; set; }

    /// <summary>
    /// License expiry date
    /// </summary>
    public DateTime? LicenseExpiryDate { get; set; }

    /// <summary>
    /// Annual license cost
    /// </summary>
    public decimal? AnnualLicenseCost { get; set; }

    // Navigation properties
    public OrganizationUnit? OwningUnit { get; set; }
    public ICollection<Process> Processes { get; set; } = new List<Process>();
    public ICollection<ProcessTask> Tasks { get; set; } = new List<ProcessTask>();
}

