using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace ESEMS.Web.Data;

/// <summary>
/// Normalizes SQL connection strings for IIS / remote SQL Server hosts.
/// </summary>
public static class SqlConnectionStringHelper
{
    private static readonly Regex NetworkLibraryClause = new(
        @"(?:^|;)Network\s+Library\s*=[^;]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Uses a <c>tcp:</c> data-source prefix for remote servers (Microsoft.Data.SqlClient 5+
    /// does not support the legacy <c>Network Library</c> keyword). Strips any existing
    /// <c>Network Library</c> clause left over from older deployments.
    /// </summary>
    public static string? PreferTcp(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        var cleaned = StripNetworkLibrary(connectionString);
        var builder = new SqlConnectionStringBuilder(cleaned);

        if (IsLocalServer(builder.DataSource))
            return builder.ConnectionString;

        var dataSource = builder.DataSource.Trim();
        if (!dataSource.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)
            && !dataSource.StartsWith("np:", StringComparison.OrdinalIgnoreCase)
            && !dataSource.StartsWith("lpc:", StringComparison.OrdinalIgnoreCase))
        {
            builder.DataSource = "tcp:" + dataSource;
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// One-off connections (e.g. pre-DI integration overlay) must not use the shared pool
    /// or they can exhaust Min Pool Size while SQL is unreachable at startup.
    /// </summary>
    public static string? ForAdHocConnection(string? connectionString, int connectTimeoutSeconds = 5)
    {
        var normalized = PreferTcp(connectionString);
        if (string.IsNullOrWhiteSpace(normalized))
            return normalized;

        var builder = new SqlConnectionStringBuilder(normalized)
        {
            Pooling = false,
            ConnectTimeout = connectTimeoutSeconds
        };
        return builder.ConnectionString;
    }

    /// <summary>Server/host portion for error messages (no password).</summary>
    public static string DescribeDataSource(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "(not configured)";
        try
        {
            return new SqlConnectionStringBuilder(connectionString).DataSource;
        }
        catch
        {
            return "(invalid connection string)";
        }
    }

    internal static string StripNetworkLibrary(string connectionString)
    {
        var stripped = NetworkLibraryClause.Replace(connectionString, "");
        return stripped.Trim().Trim(';');
    }

    private static bool IsLocalServer(string? dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource))
            return true;

        var source = dataSource.Trim();
        if (source.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            source = source[4..];
        else if (source.StartsWith("np:", StringComparison.OrdinalIgnoreCase))
            source = source[3..];

        return source.Equals(".", StringComparison.OrdinalIgnoreCase)
            || source.Equals("(local)", StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("(local)", StringComparison.OrdinalIgnoreCase)
            || source.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("localhost\\", StringComparison.OrdinalIgnoreCase)
            || source.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }
}
