using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ESEMS.Web.Services.Integrations.Contracts;
using Microsoft.Extensions.Caching.Memory;

namespace ESEMS.Web.Services.Integrations.Performance;

/// <summary>
/// Generic HTTP-client implementation of <see cref="IProcessPerformanceProvider"/>. Talks
/// to whatever external process-performance system this tenant is integrated with, at
/// the URL configured in Integrations:ProcessPerformance:BaseUrl. Endpoints expected:
///   GET {BaseUrl}/kpis/by-entity?type={entityType}&amp;id={entityId}&amp;take=N
///   GET {BaseUrl}/health
/// JSON shape documented at the bottom of this file. The display name surfaced in the
/// UI is taken from Integrations:ProcessPerformance:DisplayName so every deployment
/// labels the source however it wants without recompiling.
///
/// All failures degrade to an empty list + warning log; a flaky external system never
/// blocks a page render.
/// </summary>
public sealed class PerformanceHttpProvider : IProcessPerformanceProvider
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PerformanceHttpProvider> _log;
    private readonly ProviderOptions _opts;
    private DateTime? _lastProbeUtc;
    private string?   _lastProbeError;
    private bool      _lastProbeOk;

    public PerformanceHttpProvider(
        HttpClient http,
        IMemoryCache cache,
        ILogger<PerformanceHttpProvider> log,
        ProviderOptions opts)
    {
        _http  = http;
        _cache = cache;
        _log   = log;
        _opts  = opts;
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_opts.BaseUrl);

    public string ProviderName =>
        string.IsNullOrWhiteSpace(_opts.DisplayName) ? "External Performance System" : _opts.DisplayName;

    public async Task<LinkedSummaryDto?> GetKpiSummaryAsync(string entityType, string entityId, CancellationToken ct = default)
    {
        if (!IsEnabled) return null;
        var kpis = await GetKpisForEntityAsync(entityType, entityId, take: 100, ct);
        if (kpis.Count == 0) return new LinkedSummaryDto(0, 0, null);
        var offTrack = kpis.Count(k =>
            string.Equals(k.Status, "OffTrack", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(k.Status, "AtRisk",   StringComparison.OrdinalIgnoreCase));
        return new LinkedSummaryDto(kpis.Count, offTrack, null);
    }

    public async Task<IReadOnlyList<LinkedKpiDto>> GetKpisForEntityAsync(string entityType, string entityId, int take = 25, CancellationToken ct = default)
    {
        if (!IsEnabled) return Array.Empty<LinkedKpiDto>();
        var cacheKey = $"kpi:{entityType}:{entityId}";
        if (_opts.CacheSeconds > 0 && _cache.TryGetValue(cacheKey, out IReadOnlyList<LinkedKpiDto>? cached) && cached is not null)
            return cached.Count <= take ? cached : cached.Take(take).ToList();

        try
        {
            var path = $"kpis/by-entity?type={Uri.EscapeDataString(entityType)}&id={Uri.EscapeDataString(entityId)}&take={take}";
            var response = await _http.GetAsync(path, ct);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("External performance system returned {Status} for {Path}", (int)response.StatusCode, path);
                return Array.Empty<LinkedKpiDto>();
            }
            var raw = await response.Content.ReadFromJsonAsync<KpiListResponse>(JsonOpts, ct);
            var mapped = (raw?.Kpis ?? new List<KpiItem>()).Select(MapKpi).ToList();
            if (_opts.CacheSeconds > 0)
                _cache.Set(cacheKey, (IReadOnlyList<LinkedKpiDto>)mapped, TimeSpan.FromSeconds(_opts.CacheSeconds));
            return mapped;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.LogWarning("External performance system call timed out for {EntityType} {EntityId}", entityType, entityId);
            return Array.Empty<LinkedKpiDto>();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "External performance system call failed for {EntityType} {EntityId}", entityType, entityId);
            return Array.Empty<LinkedKpiDto>();
        }
    }

    public string? GetEntityDeepLink(string entityType, string entityId)
    {
        if (string.IsNullOrWhiteSpace(_opts.DeepLinkBaseUrl)) return null;
        return $"{_opts.DeepLinkBaseUrl.TrimEnd('/')}/Kpis/ByEntity?type={Uri.EscapeDataString(entityType)}&id={Uri.EscapeDataString(entityId)}";
    }

    public string? GetCreateUrl()
        => string.IsNullOrWhiteSpace(_opts.DeepLinkBaseUrl)
            ? null
            : $"{_opts.DeepLinkBaseUrl.TrimEnd('/')}/Kpis/Create";

    public string? GetIndexUrl()
        => string.IsNullOrWhiteSpace(_opts.DeepLinkBaseUrl)
            ? null
            : $"{_opts.DeepLinkBaseUrl.TrimEnd('/')}/Kpis";

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

    private LinkedKpiDto MapKpi(KpiItem k)
    {
        var dir = k.Direction?.ToLowerInvariant() switch
        {
            "higherbetter" => LinkedKpiDirection.HigherBetter,
            "lowerbetter"  => LinkedKpiDirection.LowerBetter,
            _              => LinkedKpiDirection.Neutral,
        };
        var deepLink = string.IsNullOrWhiteSpace(_opts.DeepLinkBaseUrl)
            ? null
            : $"{_opts.DeepLinkBaseUrl.TrimEnd('/')}/Kpis/Details/{Uri.EscapeDataString(k.Id ?? "")}";
        return new LinkedKpiDto(
            ExternalId:        k.Id ?? "",
            Code:              k.Code ?? "",
            Title:             k.Title ?? "(untitled)",
            Direction:         dir,
            Target:            k.Target,
            Actual:            k.Actual,
            UnitOfMeasure:     k.Unit,
            Period:            k.Period ?? "",
            Status:            k.Status ?? "Unknown",
            TrendVsPrevious:   k.TrendVsPrevious,
            ExternalUrl:       deepLink);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Wire shape for GET /kpis/by-entity. Expected JSON:
    /// {
    ///   "kpis": [
    ///     { "id": "...", "code": "KPI-001", "title": "...",
    ///       "direction": "HigherBetter|LowerBetter",
    ///       "target": 95.0, "actual": 87.3, "unit": "%", "period": "Q1 2026",
    ///       "status": "OnTrack|AtRisk|OffTrack", "trendVsPrevious": -1.2 },
    ///     ...
    ///   ]
    /// }
    /// </summary>
    private sealed class KpiListResponse
    {
        public List<KpiItem>? Kpis { get; set; }
    }

    private sealed class KpiItem
    {
        public string? Id              { get; set; }
        public string? Code            { get; set; }
        public string? Title           { get; set; }
        public string? Direction       { get; set; }
        public decimal? Target         { get; set; }
        public decimal? Actual         { get; set; }
        public string? Unit            { get; set; }
        public string? Period          { get; set; }
        public string? Status          { get; set; }
        public decimal? TrendVsPrevious { get; set; }
    }
}
