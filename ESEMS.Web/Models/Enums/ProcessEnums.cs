namespace ESEMS.Web.Models.Enums;

/// <summary>
/// APQC Process Classification Framework levels
/// </summary>
public enum APQCLevel
{
    Category = 1,      // Level 1 - High-level grouping
    ProcessGroup = 2,  // Level 2 - Major business capability
    Process = 3,       // Level 3 - End-to-end business process
    Activity = 4,      // Level 4 - Major step (optional)
    Task = 5           // Level 5 - Smallest unit (optional)
}

/// <summary>
/// Process type based on APQC classification
/// </summary>
public enum ProcessType
{
    Core,       // Core operating processes
    Support,    // Support processes
    Management  // Management and support services
}

/// <summary>
/// Process status in the governance lifecycle
/// </summary>
public enum ProcessStatus
{
    Draft,
    Active,
    UnderImprovement,
    Archived
}

/// <summary>
/// Service type (internal or external)
/// </summary>
public enum ServiceType
{
    Internal,
    External
}

/// <summary>
/// Channel type for activities
/// </summary>
public enum ChannelType
{
    Digital,
    Physical,
    Hybrid
}

/// <summary>
/// Time units for duration measurements. BusinessDays / Weeks / Months /
/// Years added 2026-05-18 for ServiceCatalogInfo.DurationUnit — services
/// typically quote SLAs as "15 working days" or "1 year". Underlying values
/// 0-2 unchanged so existing rows don't shift.
/// </summary>
public enum TimeUnit
{
    Minutes = 0,
    Hours = 1,
    Days = 2,
    BusinessDays = 3,
    Weeks = 4,
    Months = 5,
    Years = 6
}

/// <summary>
/// Specific delivery channels a service can be obtained through. Distinct
/// from <see cref="ChannelType"/> (Digital/Physical/Hybrid) which is the
/// macro classification — this enum is the citizen-facing menu of *exactly
/// where* to get the service. Multi-select: a service can be available on
/// Dubai Now App AND the web portal AND a service center simultaneously.
/// </summary>
public enum ServiceDeliveryChannel
{
    /// <summary>Mobile app (generic, when not tied to a specific app)</summary>
    MobileApp = 0,
    /// <summary>Dubai Now app</summary>
    DubaiNowApp = 1,
    /// <summary>UAE Smart Government portal (smartgov.ae / u.ae)</summary>
    SmartGovPortal = 2,
    /// <summary>MBRHE web portal (mbrhe.gov.ae)</summary>
    WebPortal = 3,
    /// <summary>In-person at a service center / front desk</summary>
    ServiceCenter = 4,
    /// <summary>Phone call to the contact center</summary>
    PhoneCall = 5,
    /// <summary>WhatsApp business channel</summary>
    WhatsApp = 6,
    /// <summary>Email submission</summary>
    Email = 7,
    /// <summary>Postal mail</summary>
    Mail = 8,
    /// <summary>Self-service kiosk</summary>
    Kiosk = 9
}

/// <summary>
/// Improvement measurement type (RFP Section 15)
/// </summary>
public enum ImprovementMeasurementType
{
    Satisfaction,       // Customer/stakeholder satisfaction
    Cost,               // Cost-related metric
    Time,               // Time-related metric (processing time, cycle time, etc.)
    Productivity,       // Productivity metric (output per unit)
    Capacity,           // Capacity utilization metric
    NumberOfVisits,     // Visit count metric
    NumberOfDocuments,  // Document count metric
    Custom              // User-defined measurement
}

/// <summary>
/// Improvement quadrant classification (Impact vs Effort Matrix)
/// </summary>
public enum ImprovementQuadrant
{
    QuickWins,        // High Impact, Low Effort - Immediate Actions
    MajorProjects,    // High Impact, High Effort - Transformation Work
    FillIns,          // Low Impact, Low Effort - Maintenance
    ThanklessTasks    // Low Impact, High Effort - Avoid/Deprioritize
}

/// <summary>
/// Change request status
/// </summary>
public enum ChangeRequestStatus
{
    Submitted,
    UnderReview,
    Approved,
    Rejected,
    Implemented,
    Cancelled
}

/// <summary>
/// Change request source
/// </summary>
public enum ChangeRequestSource
{
    InnovationManagementSystem,
    InternalImprovement,
    AuditFindings,
    ManagementDirectives,
    CustomerFeedback,
    ExternalRiskSystem,
    Other
}

/// <summary>
/// RACI roles
/// </summary>
public enum RACIRole
{
    Responsible,
    Accountable,
    Consulted,
    Informed
}

/// <summary>
/// Audit log action types
/// </summary>
public enum AuditAction
{
    Create,
    Update,
    Delete,
    View,
    Approve,
    Reject,
    Submit,
    Archive,
    Restore,
    VersionChange,
    Login,
    Logout
}

/// <summary>
/// Incident status (ISO 20000-1:2018)
/// </summary>
public enum IncidentStatus
{
    New,
    Acknowledged,
    InProgress,
    OnHold,
    Resolved,
    Closed,
    Cancelled
}

/// <summary>
/// Problem status (ISO 20000-1:2018)
/// </summary>
public enum ProblemStatus
{
    New,
    InvestigationInProgress,
    RootCauseIdentified,
    WorkaroundAvailable,
    PermanentSolutionImplemented,
    Resolved,
    Closed
}

/// <summary>
/// Asset status (ISO 55001:2014)
/// </summary>
public enum AssetStatus
{
    Planned,
    Ordered,
    InTransit,
    Operational,
    UnderMaintenance,
    Retired,
    Disposed
}

/// <summary>
/// Information-asset classification per ISO 27001 / Dubai PDPL.
/// Determines handling, encryption, retention, and disclosure controls.
/// Used by the Asset entity when the asset's category is an information
/// asset (database, document, dataset, application data).
///   Public       — freely shareable; no controls required
///   Internal     — staff-only; default for unmarked records
///   Confidential — restricted to named roles; encryption at rest
///   Restricted   — strictly need-to-know; encryption + access logging
/// </summary>
public enum InformationClassification
{
    Public,
    Internal,
    Confidential,
    Restricted
}

/// <summary>
/// Construction lifecycle stage for real-estate assets (housing projects,
/// villas, buildings, plots). Orthogonal to AssetStatus — AssetStatus
/// tracks operational state across all asset classes (IT, equipment,
/// real-estate); this enum tracks the build-out phase specific to
/// MBRHE's housing portfolio.
/// </summary>
public enum ConstructionStatus
{
    Design,
    Tender,
    UnderConstruction,
    ReadyForHandover,
    HandedOver,
    Occupied,
    Vacant
}

/// <summary>
/// Maintenance type (ISO 55001:2014)
/// </summary>
public enum MaintenanceType
{
    Preventive,
    Corrective,
    Predictive,
    Emergency
}

/// <summary>
/// Risk level (ISO 31000:2018)
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Feedback status (ISO 9001:2015)
/// </summary>
public enum FeedbackStatus
{
    New,
    UnderReview,
    InProgress,
    Resolved,
    Closed
}

/// <summary>
/// Feedback type (ISO 9001:2015)
/// </summary>
public enum FeedbackType
{
    Complaint,
    Suggestion,
    Compliment,
    Inquiry,
    Other
}

/// <summary>
/// Automation status for processes and procedures
/// Based on MBRHE Process Catalog classification
/// </summary>
public enum AutomationStatus
{
    /// <summary>Traditional - Manual process (تقليدية)</summary>
    Traditional,
    /// <summary>Semi-Automated - Partially automated (شبه مؤتمتة)</summary>
    SemiAutomated,
    /// <summary>Fully Automated (مؤتمتة)</summary>
    Automated
}

/// <summary>
/// Procedure status in the lifecycle
/// </summary>
public enum ProcedureStatus
{
    /// <summary>Approved and active (معتمد)</summary>
    Approved,
    /// <summary>Cancelled/Deprecated (ملغي)</summary>
    Cancelled,
    /// <summary>Under Review</summary>
    UnderReview,
    /// <summary>Draft</summary>
    Draft
}

/// <summary>
/// Current/Proposed status for process changes
/// </summary>
public enum CurrentProposedStatus
{
    /// <summary>Current active process (إجراء حالي)</summary>
    Current,
    /// <summary>Proposed new or modified process (مقترح)</summary>
    Proposed
}

/// <summary>
/// Organization unit level in hierarchy
/// </summary>
public enum OrganizationLevel
{
    /// <summary>Sector (قطاع) - Highest level</summary>
    Sector = 0,
    /// <summary>Department (إدارة)</summary>
    Department = 1,
    /// <summary>Section (قسم)</summary>
    Section = 2,
    /// <summary>Function</summary>
    Function = 3,
    /// <summary>SubFunction - Lowest level</summary>
    SubFunction = 4
}

/// <summary>
/// Organization unit classification — *what kind* of unit it is, separate
/// from <see cref="OrganizationLevel"/> which captures depth in the tree.
/// Sourced from the MBRHE org-structure sheet (column النوع). Two units at
/// the same Level can have different UnitTypes — e.g. an internal-audit
/// "Office" reports to the CEO at the same depth as a "Department" reports
/// to a Sector, but they're structurally different beasts.
/// </summary>
public enum OrganizationUnitType
{
    /// <summary>Sector (قطاع)</summary>
    Sector = 0,
    /// <summary>Department (إدارة)</summary>
    Department = 1,
    /// <summary>Section (قسم)</summary>
    Section = 2,
    /// <summary>Center (مركز) — operational service center, often parallel to Section</summary>
    Center = 3,
    /// <summary>Office (مكتب) — staff function, often direct-report to CEO</summary>
    Office = 4,
    /// <summary>Oversight Body (جهة رقابية) — Board of Directors and external auditors</summary>
    OversightBody = 5,
    /// <summary>Executive Leadership (قيادة تنفيذية) — CEO and similar</summary>
    ExecutiveLeadership = 6
}

/// <summary>
/// Automability assessment
/// </summary>
public enum AutomabilityStatus
{
    /// <summary>Can be automated (قابل للأتمتة)</summary>
    Automatable,
    /// <summary>Cannot be automated (غير قابل للأتمتة)</summary>
    NotAutomatable,
    /// <summary>Partially automatable (قابل للأتمتة بشكل جزئي)</summary>
    PartiallyAutomatable
}

/// <summary>
/// Process classification type (Main/Support)
/// Based on MBRHE Process Catalog
/// </summary>
public enum ProcessClassificationType
{
    /// <summary>Main/Primary process (رئيسية)</summary>
    Main,
    /// <summary>Support process (داعمة)</summary>
    Support,
    /// <summary>Enabling process (ممكنة)</summary>
    Enabling
}

/// <summary>
/// Lifecycle state of a <see cref="ESEMS.Web.Models.RiskManagement.RiskActionPlan"/>.
/// Replaces the prior free-text Status column (audit H4) so typos like
/// "In Progres" or "Inprogess" can't break governance dashboards.
/// </summary>
public enum RiskActionPlanStatus
{
    NotStarted,
    InProgress,
    OnTrack,
    AtRisk,
    Completed,
    Cancelled
}

/// <summary>
/// Innovation type classification (Draft8 - MBRHE Innovation Framework)
/// </summary>
public enum InnovationType
{
    /// <summary>Gradual improvements to existing processes</summary>
    Incremental,
    /// <summary>Significant advances in capability</summary>
    Breakthrough,
    /// <summary>Game-changing innovations</summary>
    Disruptive,
    /// <summary>Fundamental paradigm shifts</summary>
    Transformative
}

/// <summary>
/// Innovation horizon timeline (Draft8 - MBRHE Roadmap)
/// </summary>
public enum ImprovementHorizon
{
    /// <summary>2023-2025: Current Business Model (Enhancement)</summary>
    Horizon1_Current,
    /// <summary>2026-2028: Expand Business Model (Experimentation)</summary>
    Horizon2_Expand,
    /// <summary>2029+: Future Business Model (Imagination)</summary>
    Horizon3_Future
}

/// <summary>
/// Improvement lifecycle roles - defines who does what in the 5-step improvement process
/// Based on best practices for mature organizations
/// </summary>
public enum ImprovementLifecycleRole
{
    /// <summary>Step 1: Identify - Frontline staff, service owners, quality teams</summary>
    Identifier,
    /// <summary>Step 2: Analyze - Process specialists, data analysts, SMEs</summary>
    Analyst,
    /// <summary>Step 3: Plan - Process owner, management, project lead</summary>
    Planner,
    /// <summary>Step 4: Execute - Operational teams, implementation team, IT support</summary>
    Executor,
    /// <summary>Step 5: Monitor & Sustain - Quality office, process owner, governance team</summary>
    Monitor,
    /// <summary>Overall accountable person for the improvement initiative</summary>
    ProcessOwner,
    /// <summary>Independent monitoring and sustainability oversight</summary>
    QualityGovernance
}

/// <summary>
/// Language of a process's controlling document. Matches the values in the
/// Process Catalog reference sheet (full.xlsx → Sheet17 → "لغة الوثيقة").
/// </summary>
public enum DocumentLanguage
{
    /// <summary>Arabic only (العربية)</summary>
    Arabic,
    /// <summary>English only</summary>
    English,
    /// <summary>Both Arabic and English (ثنائي اللغة)</summary>
    Bilingual
}

/// <summary>
/// Status of a workload analysis scenario.
/// </summary>
public enum WorkloadScenarioStatus
{
    Draft,
    InReview,
    Approved,
    Archived
}


