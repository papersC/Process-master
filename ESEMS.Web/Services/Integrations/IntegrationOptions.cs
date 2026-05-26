namespace ESEMS.Web.Services.Integrations;

/// <summary>
/// Bound from the "Integrations" section of appsettings.json. Per-tenant config lives in
/// the production overrides file and the values are picked up at startup. Changing them
/// requires an app-pool recycle — that's intentional, since flipping a provider mid-flight
/// would leave half-rendered partials in caches.
/// </summary>
public sealed class IntegrationOptions
{
    public const string SectionName = "Integrations";

    public ProviderOptions Risk { get; set; } = new();
    public ProviderOptions ProcessPerformance { get; set; } = new();
}

/// <summary>One external system's worth of config.</summary>
public sealed class ProviderOptions
{
    /// <summary>Which implementation DI should pick. "None" → no-op. "Http" → the generic
    /// REST client (RiskHttpProvider / PerformanceHttpProvider). Unknown values fall back
    /// to "None" with a startup warning so a typo doesn't take down the page.</summary>
    public string Provider { get; set; } = "None";

    /// <summary>Base URL of the external API. Trailing slash is appended if missing so
    /// relative paths in the HttpClient line up. Per-tenant — set in the production
    /// appsettings, never in source-controlled defaults.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Friendly label shown next to the integration panel header in the UI
    /// (e.g. "Acme Risk Manager"). When blank, the provider falls back to a generic
    /// "External Risk System" / "External Performance System". Lets each tenant brand
    /// the source however they want without recompiling.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Shared API key sent in the X-Api-Key header. Stored in user-secrets or
    /// env-var (`Integrations__Risk__ApiKey`) — never in source-controlled appsettings.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Per-call timeout. Keep short — page render blocks on this.</summary>
    public int TimeoutSeconds { get; set; } = 8;

    /// <summary>How long to cache a successful response per (entityType, entityId) key.
    /// Set to 0 to disable caching (only useful for debugging — page renders will hit the
    /// external API on every request).</summary>
    public int CacheSeconds { get; set; } = 60;

    /// <summary>Optional UI base URL for deep links. If "", deep links are suppressed even
    /// when BaseUrl is set, because the API URL and UI URL are usually different.</summary>
    public string DeepLinkBaseUrl { get; set; } = string.Empty;
}
