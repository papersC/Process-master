using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ESEMS.Web.Services.Integrations.Contracts;
using Microsoft.Extensions.Caching.Memory;

namespace ESEMS.Web.Services.Integrations.Risk;

/// <summary>
/// Generic HTTP-client implementation of <see cref="IRiskProvider"/>. Talks to whatever
/// external risk system this tenant is integrated with, at the URL configured in
/// Integrations:Risk:BaseUrl. Endpoints expected:
///   GET {BaseUrl}/risks/by-entity?type={entityType}&amp;id={entityId}&amp;take=N
///   GET {BaseUrl}/risks?take=N                  — full enterprise list for the Index page
///   GET {BaseUrl}/health
/// JSON shape documented at the bottom of this file. The display name surfaced in the
/// UI is taken from Integrations:Risk:DisplayName so every deployment labels the source
/// however it wants without recompiling.
///
/// All failures (timeout, non-2xx, parse error) degrade to an empty list + warning log;
/// a flaky external system never blocks a page render.
/// </summary>
public sealed class RiskHttpProvider : IRiskProvider
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RiskHttpProvider> _log;
    private readonly ProviderOptions _opts;
    private DateTime? _lastProbeUtc;
    private string?   _lastProbeError;
    private bool      _lastProbeOk;

    public RiskHttpProvider(
        HttpClient http,
        IMemoryCache cache,
        ILogger<RiskHttpProvider> log,
        ProviderOptions opts)
    {
        _http  = http;
        _cache = cache;
        _log   = log;
        _opts  = opts;
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_opts.BaseUrl);

    /// <summary>
    /// Friendly source label shown next to the panel header. Comes from
    /// Integrations:Risk:DisplayName so each tenant can use whatever the integrated
    /// system calls itself; falls back to "External Risk System" when not set.
    /// </summary>
    public string ProviderName =>
        string.IsNullOrWhiteSpace(_opts.DisplayName) ? "External Risk System" : _opts.DisplayName;

    public async Task<LinkedSummaryDto?> GetRiskSummaryAsync(string entityType, string entityId, CancellationToken ct = default)
    {
        if (!IsEnabled) return null;
        var risks = await GetRisksForEntityAsync(entityType, entityId, take: 100, ct);
        if (risks.Count == 0) return new LinkedSummaryDto(0, 0, null);
        var hot = risks.Count(r => r.Severity == LinkedRiskSeverity.High || r.Severity == LinkedRiskSeverity.Critical);
        var lastUpdated = risks.Max(r => (DateTime?)r.CreatedAt);
        return new LinkedSummaryDto(risks.Count, hot, lastUpdated);
    }

    public async Task<IReadOnlyList<LinkedRiskDto>> GetRisksForEntityAsync(string entityType, string entityId, int take = 25, CancellationToken ct = default)
    {
        if (!IsEnabled) return Array.Empty<LinkedRiskDto>();
        var cacheKey = $"risk:{entityType}:{entityId}";
        if (_opts.CacheSeconds > 0 && _cache.TryGetValue(cacheKey, out IReadOnlyList<LinkedRiskDto>? cached) && cached is not null)
            return cached.Count <= take ? cached : cached.Take(take).ToList();

        try
        {
            var path = $"risks/by-entity?type={Uri.EscapeDataString(entityType)}&id={Uri.EscapeDataString(entityId)}&take={take}";
            var response = await _http.GetAsync(path, ct);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("External risk system returned {Status} for {Path}", (int)response.StatusCode, path);
                return Array.Empty<LinkedRiskDto>();
            }
            var raw = await response.Content.ReadFromJsonAsync<RiskListResponse>(JsonOpts, ct);
            var mapped = (raw?.Risks ?? new List<RiskItem>()).Select(MapRisk).ToList();
            if (_opts.CacheSeconds > 0)
                _cache.Set(cacheKey, (IReadOnlyList<LinkedRiskDto>)mapped, TimeSpan.FromSeconds(_opts.CacheSeconds));
            return mapped;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.LogWarning("External risk system call timed out for {EntityType} {EntityId}", entityType, entityId);
            return Array.Empty<LinkedRiskDto>();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "External risk system call failed for {EntityType} {EntityId}", entityType, entityId);
            return Array.Empty<LinkedRiskDto>();
        }
    }

    public async Task<IReadOnlyList<LinkedRiskDto>> GetAllRisksAsync(int take = 100, CancellationToken ct = default)
    {
        if (!IsEnabled) return Array.Empty<LinkedRiskDto>();
        var cacheKey = $"risk:all:{take}";
        if (_opts.CacheSeconds > 0 && _cache.TryGetValue(cacheKey, out IReadOnlyList<LinkedRiskDto>? cached) && cached is not null)
            return cached;

        try
        {
            var path = $"risks?take={take}";
            var response = await _http.GetAsync(path, ct);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("External risk system returned {Status} for {Path}", (int)response.StatusCode, path);
                return Array.Empty<LinkedRiskDto>();
            }
            var raw = await response.Content.ReadFromJsonAsync<RiskListResponse>(JsonOpts, ct);
            var mapped = (raw?.Risks ?? new List<RiskItem>()).Select(MapRisk).ToList();
            if (_opts.CacheSeconds > 0)
                _cache.Set(cacheKey, (IReadOnlyList<LinkedRiskDto>)mapped, TimeSpan.FromSeconds(_opts.CacheSeconds));
            return mapped;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.LogWarning("External risk system GetAllRisks call timed out");
            return Array.Empty<LinkedRiskDto>();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "External risk system GetAllRisks call failed");
            return Array.Empty<LinkedRiskDto>();
        }
    }

    public string? GetEntityDeepLink(string entityType, string entityId)
    {
        if (string.IsNullOrWhiteSpace(_opts.DeepLinkBaseUrl)) return null;
        var baseUrl = _opts.DeepLinkBaseUrl.TrimEnd('/');
        return $"{baseUrl}/Risks/ByEntity?type={Uri.EscapeDataString(entityType)}&id={Uri.EscapeDataString(entityId)}";
    }

    public string? GetCreateUrl()
    {
        if (string.IsNullOrWhiteSpace(_opts.DeepLinkBaseUrl)) return null;
        return $"{_opts.DeepLinkBaseUrl.TrimEnd('/')}/Risks/Create";
    }

    public string? GetIndexUrl()
    {
        if (string.IsNullOrWhiteSpace(_opts.DeepLinkBaseUrl)) return null;
        return $"{_opts.DeepLinkBaseUrl.TrimEnd('/')}/Risks";
    }

    public async Task<IntegrationHealth> ProbeAsync(CancellationToken ct = default)
    {
        if (!IsEnabled)
            return new IntegrationHealth(ProviderName, false, false, _lastProbeUtc, "BaseUrl not configured", _opts.BaseUrl, _opts.TimeoutSeconds, _opts.CacheSeconds);
        try
        {
            var response = await _http.GetAsync("health", ct);
            _lastProbeUtc   = DateTime.UtcNow;
            _lastProbeOk    = response.IsSuccessStatusCode;
            _lastProbeError = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}";
        }
        catch (Exception ex)
        {
            _lastProbeUtc   = DateTime.UtcNow;
            _lastProbeOk    = false;
            _lastProbeError = ex.GetType().Name + ": " + ex.Message;
        }
        return new IntegrationHealth(ProviderName, true, _lastProbeOk, _lastProbeUtc, _lastProbeError, _opts.BaseUrl, _opts.TimeoutSeconds, _opts.CacheSeconds);
    }

    private LinkedRiskDto MapRisk(RiskItem r)
    {
        var sev = r.Severity?.ToLowerInvariant() switch
        {
            "low"      => LinkedRiskSeverity.Low,
            "medium"   => LinkedRiskSeverity.Medium,
            "high"     => LinkedRiskSeverity.High,
            "critical" => LinkedRiskSeverity.Critical,
            _          => LinkedRiskSeverity.Unknown,
        };
        var deepLink = string.IsNullOrWhiteSpace(_opts.DeepLinkBaseUrl)
            ? null
            : $"{_opts.DeepLinkBaseUrl.TrimEnd('/')}/Risks/Details/{Uri.EscapeDataString(r.Id ?? "")}";
        return new LinkedRiskDto(
            ExternalId:  r.Id ?? "",
            Code:        r.Code ?? "",
            Title:       r.Title ?? "(untitled)",
            Severity:    sev,
            Status:      r.Status ?? "Unknown",
            OwnerName:   r.OwnerName,
            CreatedAt:   r.CreatedAt ?? DateTime.UtcNow,
            ExternalUrl: deepLink);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Wire shape for GET /risks/by-entity. Expected JSON:
    /// {
    ///   "risks": [
    ///     { "id": "...", "code": "RSK-001", "title": "...",
    ///       "severity": "Low|Medium|High|Critical", "status": "Open|Mitigated|Closed",
    ///       "ownerName": "...", "createdAt": "2026-04-15T10:00:00Z" },
    ///     ...
    ///   ]
    /// }
    /// The integrated system MUST honour this shape — version it as /v1/risks/by-entity
    /// if the contract ever needs to change.
    /// </summary>
    private sealed class RiskListResponse
    {
        public List<RiskItem>? Risks { get; set; }
    }

    private sealed class RiskItem
    {
        public string?   Id        { get; set; }
        public string?   Code      { get; set; }
        public string?   Title     { get; set; }
        public string?   Severity  { get; set; }
        public string?   Status    { get; set; }
        public string?   OwnerName { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
