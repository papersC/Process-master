using ESEMS.Web.Data;
using Microsoft.Data.SqlClient;

namespace ESEMS.Web.Services.Integrations;

/// <summary>
/// Reads any "Integrations.*" rows from the AppSettings table BEFORE the host is built
/// and patches them into <see cref="IConfigurationManager"/> as in-memory overrides.
/// That way <see cref="IntegrationServiceCollectionExtensions.AddIntegrations"/> sees
/// the DB-overlaid config when it constructs the provider singletons + their
/// HttpClients, so changing settings via the Settings Hub UI takes effect on the next
/// app-pool recycle without any further wiring.
///
/// Why pre-DI: the HttpClient inside each provider is configured with BaseAddress at
/// registration time. Patching the singleton ProviderOptions after registration
/// wouldn't move the HttpClient's BaseAddress. Reading from the DB before AddIntegrations
/// runs is the only way to keep one source of truth without breaking the provider model.
///
/// Failure mode: if the DB is unreachable at startup, the method logs to stderr and
/// silently returns — appsettings.json values remain in effect. The app still starts.
/// </summary>
public static class IntegrationSettingsBootstrap
{
    /// <summary>Keys to scan for. Dot-form, matching what the Settings Hub writes.</summary>
    private static readonly string[] WatchedKeyPrefixes = new[]
    {
        "Integrations.Risk.",
        "Integrations.ProcessPerformance."
    };

    /// <summary>
    /// Pull AppSettings rows whose Key starts with one of <see cref="WatchedKeyPrefixes"/>
    /// and overlay them into the configuration tree (translating "." to ":" for the
    /// ASP.NET Core hierarchical key syntax).
    /// </summary>
    public static void OverlayFromDatabase(ConfigurationManager configuration)
    {
        var connectionString = SqlConnectionStringHelper.ForAdHocConnection(
            configuration.GetConnectionString("DefaultConnection"));
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // No DB configured — fine. appsettings.json values remain authoritative.
            return;
        }

        var overrides = new Dictionary<string, string?>();
        try
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT [Key], [Value] FROM [AppSettings] WHERE [Key] LIKE @prefix";
            var p = cmd.CreateParameter();
            p.ParameterName = "@prefix";
            p.Value         = "Integrations.%";
            cmd.Parameters.Add(p);
            cmd.CommandTimeout = 5; // don't block app startup on a slow DB

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var key = reader.GetString(0);
                if (!WatchedKeyPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    continue; // belt-and-suspenders against accidental matches

                // ASP.NET Core uses ":" as the hierarchical separator; AppSettings uses "."
                // because the existing Email/Alerts/AI tabs already store keys that way.
                var configKey = key.Replace('.', ':');
                var value = reader.IsDBNull(1) ? null : reader.GetString(1);
                overrides[configKey] = value;
            }
        }
        catch (Exception ex)
        {
            // Don't take down the app if the DB is unreachable at startup. Anything we
            // already loaded from appsettings.json keeps working.
            Console.Error.WriteLine($"[IntegrationSettingsBootstrap] Skipped DB overlay: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        if (overrides.Count > 0)
        {
            configuration.AddInMemoryCollection(overrides!);
        }
    }
}
