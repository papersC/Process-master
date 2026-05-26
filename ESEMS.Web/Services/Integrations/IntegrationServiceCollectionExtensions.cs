using ESEMS.Web.Services.Integrations.Contracts;
using ESEMS.Web.Services.Integrations.NoOp;
using ESEMS.Web.Services.Integrations.Performance;
using ESEMS.Web.Services.Integrations.Risk;
using Microsoft.Extensions.Caching.Memory;

namespace ESEMS.Web.Services.Integrations;

/// <summary>
/// Single entry-point for wiring up external-system integrations. Reads the
/// "Integrations" section of configuration, validates each provider name, and binds the
/// chosen implementation as the singleton interface. The contract is: views and
/// controllers depend on IRiskProvider / IProcessPerformanceProvider only; this
/// extension picks the concrete type at startup based on per-tenant config.
///
/// Provider keys (case-insensitive):
///   "None"  → no-op (default; UI panels render nothing)
///   "Http"  → generic REST client (RiskHttpProvider / PerformanceHttpProvider)
/// Future adapters (queue-based, gRPC, vendor-specific) plug in here as additional
/// case branches without touching any view or controller.
/// </summary>
public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration, ILogger? bootstrapLog = null)
    {
        var options = new IntegrationOptions();
        configuration.GetSection(IntegrationOptions.SectionName).Bind(options);
        services.AddSingleton(options);

        // Memory cache backs both providers' per-(entityType, entityId) suppression. Reuses
        // the AddMemoryCache one if already registered — safe to call twice.
        services.AddMemoryCache();

        WireRiskProvider(services, options.Risk, bootstrapLog);
        WirePerformanceProvider(services, options.ProcessPerformance, bootstrapLog);
        return services;
    }

    private static void WireRiskProvider(IServiceCollection services, ProviderOptions opts, ILogger? log)
    {
        switch (opts.Provider?.Trim().ToLowerInvariant())
        {
            case "http":
                if (string.IsNullOrWhiteSpace(opts.BaseUrl))
                {
                    log?.LogWarning("Integrations:Risk:Provider=Http but BaseUrl is empty. Falling back to NoOp.");
                    services.AddSingleton<IRiskProvider, NoOpRiskProvider>();
                    return;
                }
                services.AddHttpClient("Integrations.Risk.Http", c => ConfigureClient(c, opts));
                services.AddSingleton<IRiskProvider>(sp => new RiskHttpProvider(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("Integrations.Risk.Http"),
                    sp.GetRequiredService<IMemoryCache>(),
                    sp.GetRequiredService<ILogger<RiskHttpProvider>>(),
                    opts));
                return;

            case null:
            case "":
            case "none":
                services.AddSingleton<IRiskProvider, NoOpRiskProvider>();
                return;

            default:
                log?.LogWarning("Unknown Integrations:Risk:Provider value '{Provider}'. Falling back to NoOp.", opts.Provider);
                services.AddSingleton<IRiskProvider, NoOpRiskProvider>();
                return;
        }
    }

    private static void WirePerformanceProvider(IServiceCollection services, ProviderOptions opts, ILogger? log)
    {
        switch (opts.Provider?.Trim().ToLowerInvariant())
        {
            case "http":
                if (string.IsNullOrWhiteSpace(opts.BaseUrl))
                {
                    log?.LogWarning("Integrations:ProcessPerformance:Provider=Http but BaseUrl is empty. Falling back to NoOp.");
                    services.AddSingleton<IProcessPerformanceProvider, NoOpProcessPerformanceProvider>();
                    return;
                }
                services.AddHttpClient("Integrations.Performance.Http", c => ConfigureClient(c, opts));
                services.AddSingleton<IProcessPerformanceProvider>(sp => new PerformanceHttpProvider(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("Integrations.Performance.Http"),
                    sp.GetRequiredService<IMemoryCache>(),
                    sp.GetRequiredService<ILogger<PerformanceHttpProvider>>(),
                    opts));
                return;

            case null:
            case "":
            case "none":
                services.AddSingleton<IProcessPerformanceProvider, NoOpProcessPerformanceProvider>();
                return;

            default:
                log?.LogWarning("Unknown Integrations:ProcessPerformance:Provider value '{Provider}'. Falling back to NoOp.", opts.Provider);
                services.AddSingleton<IProcessPerformanceProvider, NoOpProcessPerformanceProvider>();
                return;
        }
    }

    private static void ConfigureClient(HttpClient http, ProviderOptions opts)
    {
        var baseUrl = opts.BaseUrl.EndsWith('/') ? opts.BaseUrl : opts.BaseUrl + "/";
        http.BaseAddress = new Uri(baseUrl);
        http.Timeout     = TimeSpan.FromSeconds(opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 8);
        if (!string.IsNullOrWhiteSpace(opts.ApiKey))
            http.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", opts.ApiKey);
        http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ESEMS-Integration/1.0");
    }
}
