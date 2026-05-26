namespace ESEMS.Web.Services.Improvements;

/// <summary>
/// Configurable thresholds for <see cref="InitiativeStallDetectionService"/>.
/// Bound from <c>StallDetection</c> in appsettings.json. Defaults match the
/// constants the service ran with before this section existed.
/// </summary>
public class StallDetectionOptions
{
    /// <summary>Configuration section name in appsettings.json.</summary>
    public const string SectionName = "StallDetection";

    /// <summary>Hours between sweeps. Defaults to 12. Clamped to ≥1 at startup.</summary>
    public int PollIntervalHours { get; set; } = 12;

    /// <summary>Inactivity days to enter the Amber band. Default 14.</summary>
    public int AmberDays { get; set; } = 14;

    /// <summary>Inactivity days to enter the Red band. Default 30.</summary>
    public int RedDays { get; set; } = 30;

    /// <summary>Inactivity days to enter the Critical band. Default 60.</summary>
    public int CriticalDays { get; set; } = 60;
}
