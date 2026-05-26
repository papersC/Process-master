using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Models.Improvement;
using ESEMS.Web.Models.ServiceManagement;

namespace ESEMS.Web.Models.AssetManagement;

/// <summary>
/// Represents an asset in the Asset Management system (ISO 55001:2014)
/// </summary>
public class Asset : AuditableBilingualEntity, Common.IAssignedToUnit
{
    /// <summary>
    /// Asset tag/number (unique identifier)
    /// </summary>
    public string AssetTag { get; set; } = string.Empty;

    /// <summary>
    /// Serial number
    /// </summary>
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Asset category ID
    /// </summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>
    /// Manufacturer
    /// </summary>
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Model
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Current status
    /// </summary>
    public AssetStatus Status { get; set; } = AssetStatus.Planned;

    /// <summary>
    /// Location
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Assigned to organization unit ID
    /// </summary>
    public int? AssignedToUnitId { get; set; }

    /// <summary>
    /// Assigned to user ID
    /// </summary>
    public string? AssignedToUserId { get; set; }

    /// <summary>
    /// Related process ID
    /// </summary>
    public string? ProcessId { get; set; }

    /// <summary>
    /// Purchase date
    /// </summary>
    public DateTime? PurchaseDate { get; set; }

    /// <summary>
    /// Purchase cost
    /// </summary>
    public decimal? PurchaseCost { get; set; }

    /// <summary>
    /// Current value
    /// </summary>
    public decimal? CurrentValue { get; set; }

    /// <summary>
    /// Depreciation rate (percentage per year)
    /// </summary>
    public decimal? DepreciationRate { get; set; }

    /// <summary>
    /// Warranty expiry date
    /// </summary>
    public DateTime? WarrantyExpiryDate { get; set; }

    /// <summary>
    /// Expected end of life date
    /// </summary>
    public DateTime? EndOfLifeDate { get; set; }

    /// <summary>
    /// Last maintenance date
    /// </summary>
    public DateTime? LastMaintenanceDate { get; set; }

    /// <summary>
    /// Next scheduled maintenance date
    /// </summary>
    public DateTime? NextMaintenanceDate { get; set; }

    /// <summary>
    /// Criticality level (1=Critical, 2=High, 3=Medium, 4=Low)
    /// </summary>
    public int Criticality { get; set; } = 3;

    /// <summary>
    /// Notes
    /// </summary>
    public string? Notes { get; set; }

    // ────────────────────────────────────────────────────────────────────
    // Real-estate fields. Nullable so they stay invisible / inert for
    // IT / equipment assets. Populated when the asset's Category is a
    // real-estate category (Housing Project, Villa, Building, Plot).
    // MBRHE's housing portfolio (~79 projects per client data) is the
    // primary asset class, so these fields live on the canonical Asset
    // entity rather than a sibling table — lists, dashboards, and the
    // maintenance/risk flows all keep working as-is.
    // ────────────────────────────────────────────────────────────────────

    /// <summary>Plot or parcel number from the title deed / land registry.</summary>
    public string? PlotNumber { get; set; }

    /// <summary>Emirate (Dubai, Abu Dhabi, Sharjah, …). Free text for now;
    /// promote to enum only if reporting needs it.</summary>
    public string? Emirate { get; set; }

    /// <summary>District / community name (e.g. Al Quoz, Hatta, Nadd Al Shiba).</summary>
    public string? District { get; set; }

    /// <summary>Title deed / land registration reference number.</summary>
    public string? TitleDeedNumber { get; set; }

    /// <summary>Built-up area in square metres (only meaningful for buildings/villas).</summary>
    public decimal? BuiltUpAreaSqm { get; set; }

    /// <summary>Land / plot area in square metres.</summary>
    public decimal? LandAreaSqm { get; set; }

    /// <summary>Floor count for buildings.</summary>
    public int? Floors { get; set; }

    /// <summary>Number of dwelling units (e.g. flats in a building, villas in a project).</summary>
    public int? Units { get; set; }

    /// <summary>Bedroom count for a single villa / unit.</summary>
    public int? Bedrooms { get; set; }

    /// <summary>Construction lifecycle stage. Null when the asset isn't real-estate.</summary>
    public ConstructionStatus? ConstructionStatus { get; set; }

    /// <summary>FK to a parent housing-project Asset. A villa rolls up to the project
    /// it belongs to; a building to its development. Self-referencing FK so the
    /// hierarchy lives in the same table.</summary>
    public string? ParentProjectId { get; set; }

    /// <summary>GPS latitude (decimal degrees, WGS84). Optional.</summary>
    [Range(typeof(decimal), "-90", "90",
        ConvertValueInInvariantCulture = true, ParseLimitsInInvariantCulture = true)]
    public decimal? GpsLatitude { get; set; }

    /// <summary>GPS longitude (decimal degrees, WGS84). Optional.</summary>
    [Range(typeof(decimal), "-180", "180",
        ConvertValueInInvariantCulture = true, ParseLimitsInInvariantCulture = true)]
    public decimal? GpsLongitude { get; set; }

    // ────────────────────────────────────────────────────────────────────
    // Information-asset fields (ISO 27001 / DGEP / Dubai PDPL). Nullable
    // so they stay invisible / inert for non-information assets. Populated
    // when the asset's Category is an information category (database,
    // document, dataset, application data). The Asset entity becomes the
    // single register that satisfies both ISO 55001 (physical assets) and
    // the information-asset inventory clause of ISO 27001 A.5.9 — no
    // sibling table, no duplicated risk / maintenance / owner plumbing.
    // ────────────────────────────────────────────────────────────────────

    /// <summary>Security/sensitivity classification per ISO 27001 / PDPL.
    /// Drives default handling, encryption, retention, and disclosure rules.</summary>
    public InformationClassification? Classification { get; set; }

    /// <summary>Data owner — the user accountable for what's in the asset
    /// (decides who can access it, when it's destroyed). FK to CustomUser.UserId.</summary>
    public int? DataOwnerUserId { get; set; }

    /// <summary>Data custodian — the user responsible for protecting the asset
    /// day-to-day (backups, access reviews, encryption). FK to CustomUser.UserId.</summary>
    public int? DataCustodianUserId { get; set; }

    /// <summary>Retention period in months. Null = no policy set.
    /// Compared against CreatedAt / last-modified to drive disposal reminders.</summary>
    public int? RetentionMonths { get; set; }

    /// <summary>Comma-separated regulatory tags applicable to this asset
    /// (e.g. "PII,PDPL,FinancialRecord"). Free-text by design so new
    /// regimes can be added without a code change.</summary>
    public string? RegulatoryTags { get; set; }

    /// <summary>System or platform where the data lives (e.g. "SAP S/4HANA",
    /// "SharePoint Online", "Azure Blob — esems-archive"). Free-text so it
    /// can capture vendor systems that aren't otherwise modelled.</summary>
    public string? StorageSystem { get; set; }

    /// <summary>Storage format (e.g. "SQL database", "PDF document set",
    /// "JSONL dataset", "REST API"). Free-text for the same reason as
    /// StorageSystem.</summary>
    public string? DataFormat { get; set; }

    /// <summary>Approximate record / file count. Optional — populate when
    /// it matters for risk weighting.</summary>
    public long? RecordCount { get; set; }

    // Navigation properties
    public AssetCategory? Category { get; set; }
    /// <summary>Parent housing-project asset (self-FK on ParentProjectId).</summary>
    public Asset? ParentProject { get; set; }
    /// <summary>Child assets that roll up to this project (e.g. villas in a project).</summary>
    public ICollection<Asset> ChildAssets { get; set; } = new List<Asset>();
    public OrganizationUnit? AssignedToUnit { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? AssignedToUser { get; set; }
    public Process? Process { get; set; }
    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
    public ICollection<MaintenanceSchedule> MaintenanceSchedules { get; set; } = new List<MaintenanceSchedule>();

    // Relationship collections
    public ICollection<AssetRisk> AssetRisks { get; set; } = new List<AssetRisk>();
    public ICollection<ServiceAsset> ServiceAssets { get; set; } = new List<ServiceAsset>();
    public ICollection<ChangeRequestAsset> ChangeRequestAssets { get; set; } = new List<ChangeRequestAsset>();

    // Reverse navigation properties (Asset as parent)
    public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
    public ICollection<Problem> Problems { get; set; } = new List<Problem>();

    /// <summary>
    /// Calculate current value based on depreciation
    /// </summary>
    public void CalculateCurrentValue()
    {
        if (PurchaseCost.HasValue && PurchaseDate.HasValue && DepreciationRate.HasValue)
        {
            var yearsOwned = (DateTime.UtcNow - PurchaseDate.Value).TotalDays / 365.25;
            var depreciationAmount = PurchaseCost.Value * (DepreciationRate.Value / 100) * (decimal)yearsOwned;
            CurrentValue = Math.Max(0, PurchaseCost.Value - depreciationAmount);
        }
    }

    /// <summary>
    /// Check if warranty is still valid
    /// </summary>
    public bool IsWarrantyValid()
    {
        return WarrantyExpiryDate.HasValue && WarrantyExpiryDate.Value > DateTime.UtcNow;
    }

    /// <summary>
    /// True when the asset is past its scheduled maintenance date — used
    /// for "overdue now" UI cues (e.g. red border on the Details page).
    /// </summary>
    public bool IsMaintenanceDue()
    {
        return NextMaintenanceDate.HasValue && NextMaintenanceDate.Value <= DateTime.UtcNow;
    }

    /// <summary>
    /// True when ANY active maintenance schedule is coming due within the
    /// given window. Used for the "Maintenance Due" KPI on both the asset
    /// dashboard and list — both pages MUST use this so the same number
    /// shows in both places.
    /// </summary>
    public bool IsMaintenanceDueSoon(int withinDays = 30)
    {
        var horizon = DateTime.UtcNow.Date.AddDays(withinDays);
        return MaintenanceSchedules.Any(s => s.IsActive && s.NextScheduledDate <= horizon);
    }
}

