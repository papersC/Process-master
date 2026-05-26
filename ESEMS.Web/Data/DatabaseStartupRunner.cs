using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace ESEMS.Web.Data;

/// <summary>
/// Creates the database catalog, applies EF migrations, and logs clearly when something fails.
/// </summary>
public static class DatabaseStartupRunner
{
    /// <summary>Set when early bootstrap was skipped because of ContinueOnMigrationFailure.</summary>
    public static bool EarlyBootstrapSkipped { get; private set; }

    /// <summary>
    /// Creates the catalog named in the connection string when it does not exist yet.
    /// The login must have CREATE DATABASE (dbcreator or sysadmin) on the server.
    /// </summary>
    public static async Task EnsureDatabaseExistsAsync(
        ApplicationDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var creator = context.GetService<IRelationalDatabaseCreator>();
        if (await creator.ExistsAsync(cancellationToken))
            return;

        var database = GetDatabaseName(context);
        logger.LogWarning(
            "Database catalog {Database} does not exist on {Server} — creating it now.",
            database,
            GetServerName(context));
        await creator.CreateAsync(cancellationToken);
        logger.LogInformation("Database catalog {Database} was created.", database);
    }

    /// <summary>
    /// Returns true when the catalog is missing or there are pending EF migrations.
    /// </summary>
    public static async Task<bool> NeedsInitializationAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        var creator = context.GetService<IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync(cancellationToken))
            return true;

        try
        {
            return (await context.Database.GetPendingMigrationsAsync(cancellationToken)).Any();
        }
        catch (SqlException)
        {
            return true;
        }
    }

    /// <summary>
    /// Create catalog (if needed), pre-migration drift tables, then apply all pending migrations.
    /// </summary>
    public static async Task ApplyMigrationsPipelineAsync(
        ApplicationDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        LogConnectionTarget(context, logger);

        await EnsureDatabaseExistsAsync(context, logger, cancellationToken);
        await PreMigrationBootstrap.EnsureSchemaAsync(context, cancellationToken);

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying {Count} pending EF migration(s)...", pending.Count);
            await context.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            logger.LogInformation("No pending EF migrations.");
        }

        var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        if (applied.Count == 0)
        {
            throw new InvalidOperationException(
                $"Database {GetDatabaseName(context)} exists but no migrations were applied — " +
                "check SQL permissions and ConnectionStrings:DefaultConnection on IIS.");
        }

        logger.LogInformation(
            "Database {Database} on {Server} is ready ({MigrationCount} migration(s) applied).",
            GetDatabaseName(context),
            GetServerName(context),
            applied.Count);
    }

    public static async Task RunWithDatabaseAsync(
        WebApplication app,
        Func<ApplicationDbContext, ILogger, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        var config = app.Configuration;
        var env = app.Environment;
        var logger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseStartup");

        EarlyBootstrapSkipped = false;

        if (!config.GetValue("Database:ApplyMigrationsOnStartup", true))
        {
            logger.LogInformation("Database:ApplyMigrationsOnStartup is false — skipping early database bootstrap.");
            EarlyBootstrapSkipped = true;
            return;
        }

        var maxAttempts = Math.Clamp(config.GetValue("Database:StartupRetryCount", 6), 1, 20);
        // Default false: a running site with no database hides deploy failures.
        var continueOnFailure = config.GetValue("Database:ContinueOnMigrationFailure", false);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var scope = app.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await action(context, logger, cancellationToken);
                logger.LogInformation("Early database bootstrap completed on attempt {Attempt}.", attempt);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                logger.LogWarning(
                    ex,
                    "Database bootstrap failed (attempt {Attempt}/{MaxAttempts}). Retrying in {DelaySeconds:F0}s.",
                    attempt,
                    maxAttempts,
                    delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                LogSqlConnectivityHint(config, logger, ex);
                if (continueOnFailure)
                {
                    EarlyBootstrapSkipped = true;
                    logger.LogCritical(
                        "Database:ContinueOnMigrationFailure is true — the site will start WITHOUT a database. " +
                        "A fallback migration pass will run later; fix SQL connectivity and recycle the app pool.");
                    return;
                }

                throw;
            }
        }
    }

    private static void LogSqlConnectivityHint(IConfiguration config, ILogger logger, Exception ex)
    {
        var server = SqlConnectionStringHelper.DescribeDataSource(
            config.GetConnectionString("DefaultConnection"));
        logger.LogError(
            ex,
            "Database bootstrap failed. Configured SQL host is {SqlHost}. " +
            "From this app server, verify the hostname resolves (nslookup/ping) and SQL accepts TCP connections. " +
            "Override with IIS env var ConnectionStrings__DefaultConnection if appsettings still points at another server.",
            server);

        if (ContainsError(ex, 11001))
        {
            logger.LogError(
                "DNS could not resolve '{SqlHost}'. Use the SQL hostname reachable FROM THIS web server " +
                "(e.g. Data Source=tcp:MBRHE-ERM-Prod,1433 or localhost if SQL is on the same machine).",
                server);
        }
    }

    private static bool ContainsError(Exception ex, int number)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is SqlException sql && sql.Number == number)
                return true;
            if (current is System.ComponentModel.Win32Exception win32 && win32.NativeErrorCode == number)
                return true;
        }

        return false;
    }

    internal static bool IsTransient(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is SqlException sql)
            {
                // Do not treat login denied / permission errors as transient.
                if (sql.Number is 18456 or 262 or 2714 or 11001)
                    return false;

                if (sql.Number is 233 or -2 or 4060 or 40197 or 40501 or 49918 or 49919 or 49920)
                    return true;
            }

            if (current is System.ComponentModel.Win32Exception win32 && win32.NativeErrorCode == 233)
                return true;
        }

        return false;
    }

    private static void LogConnectionTarget(ApplicationDbContext context, ILogger logger)
    {
        logger.LogInformation(
            "Database startup targeting Server={Server}, Database={Database}.",
            GetServerName(context),
            GetDatabaseName(context));
    }

    private static string GetDatabaseName(ApplicationDbContext context)
    {
        var cs = context.Database.GetConnectionString();
        return string.IsNullOrEmpty(cs)
            ? "(unknown)"
            : new SqlConnectionStringBuilder(cs).InitialCatalog;
    }

    private static string GetServerName(ApplicationDbContext context)
    {
        var cs = context.Database.GetConnectionString();
        return string.IsNullOrEmpty(cs)
            ? "(unknown)"
            : new SqlConnectionStringBuilder(cs).DataSource;
    }
}
