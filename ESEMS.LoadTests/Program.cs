using NBomber.CSharp;
using NBomber.Contracts;
using System.Net;

namespace ESEMS.LoadTests;

/// <summary>
/// Realistic load scenario for ESEMS — anonymous + authenticated.
///
/// Profile (overridable via env vars):
///   * BASE_URL     — http://localhost:5297
///   * RAMP_USERS   — peak concurrent users (default 50)
///   * DURATION_MIN — total scenario length (default 2 minutes)
///   * AUTHED       — "1" to run authed scenario instead of anon-only
///
/// Anonymous scenario  (default):
///   /health + /Account/Login GETs at ramped concurrency.
///   Pure Kestrel + Razor pipeline floor — no DB writes, no auth.
///
/// Authenticated scenario (AUTHED=1):
///   1. Per-user cookie capture: GET /Account/Login (extract antiforgery
///      token), POST credentials, retain session cookie.
///   2. Authenticated steady-state read mix:
///      - GET /Dashboard
///      - GET /Improvements
///      - GET /EnterpriseRisks
///      - GET /Categories
///   Mirrors a typical MBRHE working hour: read-heavy on listings,
///   no writes (writes need CSRF dance per request which is more involved
///   and risks polluting the dev DB).
///
/// Run:
///   dotnet run --project ESEMS.LoadTests -c Release            # anon
///   $env:AUTHED='1'; dotnet run --project ESEMS.LoadTests -c Release   # authed
///
/// Reports land in ./reports/.
/// </summary>
public class Program
{
    private const string TestUsername = "viewer";
    private const string TestPassword = "Viewer123";

    public static int Main()
    {
        var baseUrl = (Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:5297").TrimEnd('/');
        var rampUsers = int.TryParse(Environment.GetEnvironmentVariable("RAMP_USERS"), out var ru) ? ru : 50;
        var durationMin = int.TryParse(Environment.GetEnvironmentVariable("DURATION_MIN"), out var dm) ? dm : 2;
        var authed = Environment.GetEnvironmentVariable("AUTHED") == "1";

        var scenario = authed
            ? BuildAuthedScenario(baseUrl)
            : BuildAnonScenario(baseUrl);

        scenario = scenario
            .WithoutWarmUp()
            .WithLoadSimulations(
                Simulation.RampingInject(rate: rampUsers, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(durationMin))
            );

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder("reports")
            .Run();

        return 0;
    }

    private static ScenarioProps BuildAnonScenario(string baseUrl)
    {
        var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        return Scenario.Create("esems_anon_health_and_login_landing", async ctx =>
        {
            try
            {
                using var healthResp = await http.GetAsync("/health");
                using var loginResp = await http.GetAsync("/Account/Login");
                if (!healthResp.IsSuccessStatusCode || !loginResp.IsSuccessStatusCode)
                    return Response.Fail(message: $"health={(int)healthResp.StatusCode} login={(int)loginResp.StatusCode}");
                return Response.Ok();
            }
            catch (Exception ex) { return Response.Fail(message: ex.Message); }
        });
    }

    private static ScenarioProps BuildAuthedScenario(string baseUrl)
    {
        // Per-virtual-user HttpClient so each one keeps its own cookie jar.
        return Scenario.Create("esems_authed_listing_pages", async ctx =>
        {
            var cookies = new CookieContainer();
            using var handler = new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = true };
            using var http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(10) };

            // Step 1: GET login -> harvest antiforgery token.
            var loginGet = await http.GetAsync("/Account/Login");
            if (!loginGet.IsSuccessStatusCode) return Response.Fail(message: $"login GET {(int)loginGet.StatusCode}");
            var loginHtml = await loginGet.Content.ReadAsStringAsync();
            var token = ExtractToken(loginHtml);
            if (token == null) return Response.Fail(message: "antiforgery token not found");

            // Step 2: POST credentials.
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("Username", TestUsername),
                new KeyValuePair<string,string>("Password", TestPassword),
                new KeyValuePair<string,string>("__RequestVerificationToken", token),
            });
            var loginPost = await http.PostAsync("/Account/Login", form);
            if (loginPost.RequestMessage?.RequestUri?.AbsolutePath.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase) == true)
                return Response.Fail(message: "login redirected back to /Account/Login (bad creds?)");

            // Step 3: hit a representative read mix.
            string[] paths = { "/Dashboard", "/Improvements", "/EnterpriseRisks", "/Categories" };
            foreach (var p in paths)
            {
                using var r = await http.GetAsync(p);
                if (!r.IsSuccessStatusCode) return Response.Fail(message: $"GET {p} -> {(int)r.StatusCode}");
            }
            return Response.Ok();
        });
    }

    /// <summary>
    /// Extracts the antiforgery token from the login HTML by string-search.
    /// Brittle but adequate for load tests — a real assertion would parse DOM.
    /// </summary>
    private static string? ExtractToken(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        var idx = html.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            // ASP.NET Core also emits the order: name="..." value="..." with type after — try alt.
            const string alt = "name=\"__RequestVerificationToken\" value=\"";
            idx = html.IndexOf(alt, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += alt.Length;
        }
        else
        {
            idx += marker.Length;
        }
        var end = html.IndexOf('"', idx);
        return end < 0 ? null : html.Substring(idx, end - idx);
    }
}
