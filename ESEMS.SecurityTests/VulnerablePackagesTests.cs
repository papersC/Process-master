using System.Diagnostics;

namespace ESEMS.SecurityTests;

/// <summary>
/// Runs <c>dotnet list package --vulnerable --include-transitive</c> against
/// the ESEMS.Web project and fails the test if any High/Critical CVEs are
/// reported. Cheapest, most reliable supply-chain signal — equivalent to
/// GitHub Dependabot for offline / CI use.
/// </summary>
public class VulnerablePackagesTests
{
    private const string WebCsproj = "..\\..\\..\\..\\ESEMS.Web\\ESEMS.Web.csproj";

    [Fact]
    public void Web_Project_Has_No_HighOrCritical_Vulnerable_Packages()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"list \"{WebCsproj}\" package --vulnerable --include-transitive",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(60_000);

        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var highOrCritical = lines
            .Where(l => l.Contains("High", StringComparison.OrdinalIgnoreCase)
                     || l.Contains("Critical", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(highOrCritical.Count == 0,
            $"High/Critical vulnerable packages detected:\n{string.Join("\n", highOrCritical)}\n\nFull output:\n{stdout}\n{stderr}");
    }

    [Fact]
    public void Web_Project_Has_No_Deprecated_Packages()
    {
        // Deprecated packages aren't necessarily vulnerable, but they no longer
        // get security updates — a Critical CVE in a deprecated package will
        // never be fixed. Fail only on "Legacy" reason (truly abandoned), not
        // on rebrands.
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"list \"{WebCsproj}\" package --deprecated",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit(60_000);

        var legacyHits = stdout.Split('\n')
            .Where(l => l.Contains("Legacy", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(legacyHits.Count == 0,
            $"Abandoned (Legacy-deprecated) packages detected:\n{string.Join("\n", legacyHits)}");
    }
}
