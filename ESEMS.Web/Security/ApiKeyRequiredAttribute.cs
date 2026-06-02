using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ESEMS.Web.Security;

/// <summary>
/// Authorization filter for the OUTBOUND read API (<c>api/v1/*</c>) that lets
/// other systems on the MBRHE server consume data FROM this ESEMS instance.
///
/// Validates the <c>X-Api-Key</c> request header against the keys configured
/// under <c>OutboundApi</c> — the same header convention ESEMS uses when it
/// READS from the external Risk / Performance systems
/// (see <c>IntegrationServiceCollectionExtensions.ConfigureClient</c>), so the
/// inbound and outbound sides are symmetric.
///
/// Valid keys come from (any of):
///   • <c>OutboundApi:ApiKeys</c>  (string array)
///   • <c>OutboundApi:ApiKey</c>   (single string)
///   • the <c>OUTBOUND_API_KEY</c> environment variable
/// Comparison is constant-time. The whole feature is gated by
/// <c>OutboundApi:Enabled</c>; when false every request gets 503.
///
/// Applied alongside <c>[AllowAnonymous]</c> on the controller so the global
/// cookie FallbackPolicy is bypassed and THIS filter is the sole gate. Custom
/// IAuthorizationFilters run even under [AllowAnonymous].
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApiKeyRequiredAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string HeaderName = "X-Api-Key";

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();

        if (!config.GetValue("OutboundApi:Enabled", false))
        {
            context.Result = new ObjectResult(new { error = "Outbound API is disabled." }) { StatusCode = 503 };
            return Task.CompletedTask;
        }

        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(provided))
        {
            context.Result = new ObjectResult(new { error = "Missing X-Api-Key header." }) { StatusCode = 401 };
            return Task.CompletedTask;
        }

        var configured = GetConfiguredKeys(config).ToList();
        var ok = configured.Count > 0 && configured.Any(k => FixedTimeEquals(k, provided.Trim()));
        if (!ok)
            context.Result = new ObjectResult(new { error = "Invalid API key." }) { StatusCode = 401 };

        return Task.CompletedTask;
    }

    private static IEnumerable<string> GetConfiguredKeys(IConfiguration config)
    {
        var keys = new List<string>();

        var arr = config.GetSection("OutboundApi:ApiKeys").Get<string[]>();
        if (arr != null) keys.AddRange(arr);

        var single = config["OutboundApi:ApiKey"];
        if (!string.IsNullOrWhiteSpace(single)) keys.Add(single);

        var env = Environment.GetEnvironmentVariable("OUTBOUND_API_KEY");
        if (!string.IsNullOrWhiteSpace(env)) keys.Add(env);

        return keys.Where(k => !string.IsNullOrWhiteSpace(k))
                   .Select(k => k.Trim())
                   .Distinct(StringComparer.Ordinal);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
