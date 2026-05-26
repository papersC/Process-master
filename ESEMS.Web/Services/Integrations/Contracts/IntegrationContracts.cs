namespace ESEMS.Web.Services.Integrations.Contracts;

/// <summary>
/// Marker for entity types that an external integration can be queried about. The strings
/// are stable identifiers that the external system MUST recognise, so changing one is a
/// breaking change for both sides — keep them lowercase, hyphenless, English.
/// </summary>
public static class LinkedEntityType
{
    public const string Process     = "process";
    public const string Service     = "service";
    public const string Improvement = "improvement";
    public const string Asset       = "asset";
    public const string AssetCategory = "assetcategory";
    public const string Initiative  = "initiative";
    public const string Workload    = "workloadscenario";
}

/// <summary>
/// Risk severity ladder, normalised across providers. The HTTP client mapper translates
/// whatever the external system sends into this enum so the views render with one palette.
/// </summary>
public enum LinkedRiskSeverity { Low, Medium, High, Critical, Unknown }

/// <summary>
/// Performance-direction polarity for a KPI, matching the existing Improvements
/// MeasurementDirection pattern so the views can reuse the same arrow + colour rules.
/// </summary>
public enum LinkedKpiDirection { HigherBetter, LowerBetter, Neutral }

/// <summary>One risk record, projected into the shape ESEMS needs for display.</summary>
public sealed record LinkedRiskDto(
    string  ExternalId,
    string  Code,
    string  Title,
    LinkedRiskSeverity Severity,
    string  Status,
    string? OwnerName,
    DateTime CreatedAt,
    string? ExternalUrl
);

/// <summary>One KPI record, projected into the shape ESEMS needs for display.</summary>
public sealed record LinkedKpiDto(
    string  ExternalId,
    string  Code,
    string  Title,
    LinkedKpiDirection Direction,
    decimal? Target,
    decimal? Actual,
    string?  UnitOfMeasure,
    string   Period,         // e.g. "Q1 2026", "Apr 2026"
    string   Status,         // e.g. "OnTrack", "AtRisk", "OffTrack"
    decimal? TrendVsPrevious,// positive = up vs prev period
    string?  ExternalUrl
);

/// <summary>Aggregate counts for a card-header chip — one cheap call per page render.</summary>
public sealed record LinkedSummaryDto(
    int Total,
    int OpenOrCritical,      // for risks: critical+high; for KPIs: off-track
    DateTime? LastUpdatedUtc
);

/// <summary>
/// Health snapshot of one integration, surfaced on the /Admin/Integrations page.
/// Built once per call — never cached, since the whole point is to detect staleness.
/// </summary>
public sealed record IntegrationHealth(
    string ProviderName,    // e.g. "Net", "PManagement", "None"
    bool   IsEnabled,
    bool   IsReachable,     // false if last probe failed
    DateTime? LastProbeUtc,
    string? LastError,
    string? BaseUrl,
    int    TimeoutSeconds,
    int    CacheSeconds
);

/// <summary>
/// Risk system provider. Implementations live next to this contract. The base class for
/// every page that wants to render an external risks panel takes IRiskProvider via DI;
/// when no provider is configured, the no-op implementation returns IsEnabled=false and
/// the partial renders nothing.
/// </summary>
public interface IRiskProvider
{
    /// <summary>True when the provider is configured AND its config is well-formed. The
    /// view layer checks this before doing any work — never call other methods if false.</summary>
    bool IsEnabled { get; }

    /// <summary>Display name shown next to the panel header. Comes from
    /// Integrations:Risk:DisplayName so each tenant labels the source however the
    /// integrated system calls itself; falls back to a generic "External Risk System"
    /// when not set.</summary>
    string ProviderName { get; }

    /// <summary>Cheap aggregate count for the card chip.</summary>
    Task<LinkedSummaryDto?> GetRiskSummaryAsync(string entityType, string entityId, CancellationToken ct = default);

    /// <summary>Full list for the panel body — capped at <paramref name="take"/> on the
    /// client side so a runaway external response can't stall the page.</summary>
    Task<IReadOnlyList<LinkedRiskDto>> GetRisksForEntityAsync(string entityType, string entityId, int take = 25, CancellationToken ct = default);

    /// <summary>All risks across the enterprise, paginated by <paramref name="take"/>.
    /// Used by the Risk Management module's Index/list page when the external system
    /// owns the source of truth (IsEnabled == true). The local risks table is bypassed
    /// in that mode so the user sees one consistent view.</summary>
    Task<IReadOnlyList<LinkedRiskDto>> GetAllRisksAsync(int take = 100, CancellationToken ct = default);

    /// <summary>Deep-link to open the linked entity inside the external risk system. Returns
    /// null when the provider has no UI URL or doesn't expose deep links.</summary>
    string? GetEntityDeepLink(string entityType, string entityId);

    /// <summary>Deep-link to the external risk system's "Create new risk" page. Returns
    /// null when the provider isn't configured or has no UI URL. The "Add Risk" button
    /// in the local module redirects to this URL in external mode.</summary>
    string? GetCreateUrl();

    /// <summary>Deep-link to the external risk system's "all risks" / index page. Returns
    /// null when the provider isn't configured or has no UI URL. Surfaced in the local
    /// Index view as "Manage in {ProviderName}" so the user can jump out for
    /// operations that ESEMS doesn't proxy.</summary>
    string? GetIndexUrl();

    /// <summary>One-shot health probe. Returns false if the external API is unreachable
    /// or returns a non-2xx within the configured timeout.</summary>
    Task<IntegrationHealth> ProbeAsync(CancellationToken ct = default);
}

/// <summary>
/// Process-performance system provider. Same contract shape as IRiskProvider so the wiring,
/// caching, partials, and admin-status code follow one pattern.
/// </summary>
public interface IProcessPerformanceProvider
{
    bool IsEnabled { get; }
    string ProviderName { get; }
    Task<LinkedSummaryDto?> GetKpiSummaryAsync(string entityType, string entityId, CancellationToken ct = default);
    Task<IReadOnlyList<LinkedKpiDto>> GetKpisForEntityAsync(string entityType, string entityId, int take = 25, CancellationToken ct = default);
    string? GetEntityDeepLink(string entityType, string entityId);

    /// <summary>Deep-link to the external performance system's "Create new KPI / objective" page.
    /// Returns null when the provider isn't configured or has no UI URL. The "Add" buttons in
    /// the local KPI Library and Strategic Objectives modules redirect here in external mode.</summary>
    string? GetCreateUrl();

    /// <summary>Deep-link to the external performance system's home / index page. Surfaced in
    /// the KPI Library + Strategic Objectives modules as "Manage in {ProviderName}" so the user
    /// can jump out for CRUD ESEMS doesn't proxy.</summary>
    string? GetIndexUrl();

    Task<IntegrationHealth> ProbeAsync(CancellationToken ct = default);
}
