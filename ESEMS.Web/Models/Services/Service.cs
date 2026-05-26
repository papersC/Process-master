using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.ServiceManagement;

namespace ESEMS.Web.Models.Services;

/// <summary>
/// Service entity representing internal or external services
/// </summary>
public class Service : MeasurableEntity, Common.IOwnedByUnit
{
    /// <summary>
    /// Service code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Service type (Internal or External)
    /// </summary>
    public ServiceType ServiceType { get; set; } = ServiceType.External;

    /// <summary>
    /// Channel type (Digital, Physical, Hybrid)
    /// </summary>
    public ChannelType Channel { get; set; } = ChannelType.Hybrid;

    /// <summary>
    /// FK to the ServiceCategory lookup. Canonical classification — dashboards
    /// and reports should join on this. Replaces the legacy free-text
    /// CategoryEn/CategoryAr pair that was dropped in 20260519_DropLegacyServiceCategoryColumns.
    /// </summary>
    public string? ServiceCategoryId { get; set; }
    public ServiceCategory? ServiceCategory { get; set; }

    /// <summary>
    /// Display order for sorting
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Owning organizational unit ID
    /// </summary>
    public int? OwningUnitId { get; set; }

    /// <summary>
    /// Linked strategic objective ID (DEPRECATED — use ServiceStrategicObjectives M2M).
    /// Kept on the table so legacy rows survive; no longer exposed on Create/Edit forms.
    /// </summary>
    [Obsolete("Use ServiceStrategicObjectives M2M — the M2M is canonical. StrategicObjectiveId remains as a deprecated single-link shim for legacy data.")]
    public string? StrategicObjectiveId { get; set; }

    /// <summary>
    /// Service Level Agreement (SLA) in days
    /// </summary>
    public int? SLADays { get; set; }

    /// <summary>
    /// Target delivery time in days
    /// </summary>
    public decimal? TargetDeliveryDays { get; set; }

    /// <summary>
    /// Actual average delivery time in days
    /// </summary>
    public decimal? ActualDeliveryDays { get; set; }

    /// <summary>
    /// Service fee (if applicable)
    /// </summary>
    public decimal? ServiceFee { get; set; }

    /// <summary>
    /// Whether the service is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Multiple configurable tags (comma-separated)
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Customer satisfaction score (0-100)
    /// </summary>
    public decimal? CustomerSatisfactionScore { get; set; }

    /// <summary>
    /// Number of transactions/requests per year
    /// </summary>
    public int? AnnualTransactionCount { get; set; }

    // Navigation properties
    public OrganizationUnit? OwningUnit { get; set; }

    /// <summary>
    /// Single objective nav (DEPRECATED — use ServiceStrategicObjectives collection).
    /// Still consumed by Details/Index fallback rendering for legacy rows.
    /// </summary>
    [Obsolete("Use ServiceStrategicObjectives M2M for many-to-many.")]
    public StrategicObjective? StrategicObjective { get; set; }

    /// <summary>
    /// One-to-many relationship (DEPRECATED - Use ProcessServices collection instead)
    /// </summary>
    [Obsolete("Use ProcessServices collection for many-to-many relationship")]
    public ICollection<Process> Processes { get; set; } = new List<Process>();

    public ICollection<ServiceMeasurement> Measurements { get; set; } = new List<ServiceMeasurement>();

    // Relationship collections
    public ICollection<ServiceAsset> ServiceAssets { get; set; } = new List<ServiceAsset>();
    public ICollection<ServiceRisk> ServiceRisks { get; set; } = new List<ServiceRisk>();

    /// <summary>
    /// Many-to-many relationship with Processes
    /// </summary>
    public ICollection<ProcessService> ProcessServices { get; set; } = new List<ProcessService>();

    /// <summary>
    /// Many-to-many relationship with Strategic Objectives
    /// </summary>
    public ICollection<ServiceStrategicObjective> ServiceStrategicObjectives { get; set; } = new List<ServiceStrategicObjective>();

    /// <summary>
    /// Many-to-many — chartered OrganizationUnit responsibilities this service fulfills.
    /// </summary>
    public ICollection<ServiceResponsibility> ServiceResponsibilities { get; set; } = new List<ServiceResponsibility>();

    /// <summary>
    /// 1:1 catalog sidecar — long-form bilingual content published to citizens
    /// (duration narrative, fees narrative, eligibility, pre-conditions,
    /// policies, procedure, channels, category). Lifecycle independent from
    /// the operational Service via <see cref="ServiceCatalogInfo.IsPublished"/>.
    /// </summary>
    public ServiceCatalogInfo? CatalogInfo { get; set; }

    // Reverse navigation properties (Service as parent)
    public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
    public ICollection<Problem> Problems { get; set; } = new List<Problem>();

    /// <summary>
    /// Gets the list of tags
    /// </summary>
    public List<string> GetTagList()
    {
        if (string.IsNullOrEmpty(Tags))
            return new List<string>();
        return Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                   .Select(t => t.Trim())
                   .ToList();
    }

    /// <summary>
    /// Sets tags from a list
    /// </summary>
    public void SetTagList(IEnumerable<string> tags)
    {
        Tags = string.Join(",", tags.Where(t => !string.IsNullOrWhiteSpace(t)));
    }
}

