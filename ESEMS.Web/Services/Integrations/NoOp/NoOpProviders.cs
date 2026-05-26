using ESEMS.Web.Services.Integrations.Contracts;

namespace ESEMS.Web.Services.Integrations.NoOp;

/// <summary>
/// Default risk provider when no external risk system is configured for this tenant.
/// Every method is a free no-op: IsEnabled=false short-circuits all view code, and the
/// data methods return empty results so any caller that forgets the IsEnabled gate still
/// degrades safely. There is no logging here — this is the expected steady state for
/// tenants without the integration.
/// </summary>
public sealed class NoOpRiskProvider : IRiskProvider
{
    public bool IsEnabled => false;
    public string ProviderName => "None";

    public Task<LinkedSummaryDto?> GetRiskSummaryAsync(string entityType, string entityId, CancellationToken ct = default)
        => Task.FromResult<LinkedSummaryDto?>(null);

    public Task<IReadOnlyList<LinkedRiskDto>> GetRisksForEntityAsync(string entityType, string entityId, int take = 25, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LinkedRiskDto>>(Array.Empty<LinkedRiskDto>());

    public Task<IReadOnlyList<LinkedRiskDto>> GetAllRisksAsync(int take = 100, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LinkedRiskDto>>(Array.Empty<LinkedRiskDto>());

    public string? GetEntityDeepLink(string entityType, string entityId) => null;
    public string? GetCreateUrl() => null;
    public string? GetIndexUrl() => null;

    public Task<IntegrationHealth> ProbeAsync(CancellationToken ct = default)
        => Task.FromResult(new IntegrationHealth(
            ProviderName: "None",
            IsEnabled:    false,
            IsReachable:  true,
            LastProbeUtc: DateTime.UtcNow,
            LastError:    null,
            BaseUrl:      null,
            TimeoutSeconds: 0,
            CacheSeconds:   0));
}

/// <summary>Mirror of NoOpRiskProvider for the process-performance side.</summary>
public sealed class NoOpProcessPerformanceProvider : IProcessPerformanceProvider
{
    public bool IsEnabled => false;
    public string ProviderName => "None";

    public Task<LinkedSummaryDto?> GetKpiSummaryAsync(string entityType, string entityId, CancellationToken ct = default)
        => Task.FromResult<LinkedSummaryDto?>(null);

    public Task<IReadOnlyList<LinkedKpiDto>> GetKpisForEntityAsync(string entityType, string entityId, int take = 25, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LinkedKpiDto>>(Array.Empty<LinkedKpiDto>());

    public string? GetEntityDeepLink(string entityType, string entityId) => null;
    public string? GetCreateUrl() => null;
    public string? GetIndexUrl() => null;

    public Task<IntegrationHealth> ProbeAsync(CancellationToken ct = default)
        => Task.FromResult(new IntegrationHealth(
            ProviderName: "None",
            IsEnabled:    false,
            IsReachable:  true,
            LastProbeUtc: DateTime.UtcNow,
            LastError:    null,
            BaseUrl:      null,
            TimeoutSeconds: 0,
            CacheSeconds:   0));
}
