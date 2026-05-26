using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Services;

namespace ESEMS.Web.Models.APQC;

/// <summary>
/// A single responsibility/mandate item assigned to an OrganizationUnit.
/// One unit can carry many responsibilities (charter items, mandates, KRAs).
/// </summary>
public class OrganizationUnitResponsibility : AuditableBilingualEntity
{
    [Required]
    public int OrganizationUnitId { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(OrganizationUnitId))]
    public OrganizationUnit? OrganizationUnit { get; set; }

    // Reverse M2M — what fulfills this mandate
    public ICollection<ProcessResponsibility> LinkedProcesses { get; set; } = new List<ProcessResponsibility>();
    public ICollection<ServiceResponsibility> LinkedServices { get; set; } = new List<ServiceResponsibility>();
}
