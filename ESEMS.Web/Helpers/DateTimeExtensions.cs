namespace ESEMS.Web.Helpers;

/// <summary>
/// Display-side conversions from UTC (the storage convention) to Asia/Dubai
/// time (UTC+4, no DST). Dubai is the only place ESEMS is deployed and the
/// only audience for the UI, so all timestamps in views go through these.
///
/// Storage is unchanged — DB columns, DateTime.UtcNow in business logic, EF
/// interceptors, audit-log writes — everything stays UTC. Only the *display*
/// crosses into Dubai-local. That's the standard pattern (UTC at rest,
/// local at the edge) and the one that survives the system ever moving to
/// the cloud or a different region.
///
/// Why a fixed offset instead of TimeZoneInfo: UAE has been a fixed UTC+4
/// since 1972 with no DST, so the offset is constant. Avoids the
/// TimeZoneInfo.FindSystemTimeZoneById gotcha where Windows uses
/// "Arabian Standard Time" and Linux/macOS use "Asia/Dubai" — and avoids
/// any tzdata version skew between platforms.
/// </summary>
public static class DateTimeExtensions
{
    private static readonly TimeSpan DubaiOffset = TimeSpan.FromHours(4);

    /// <summary>
    /// Convert any DateTime (UTC, Local, or Unspecified — Unspecified is
    /// treated as UTC since that's the codebase convention) to Dubai-local
    /// time, returned as Unspecified-kind so downstream serializers (System
    /// .Text.Json, Newtonsoft, EF Core) don't shift it back to UTC.
    /// </summary>
    public static DateTime ToDubai(this DateTime dt)
    {
        var asUtc = dt.Kind switch
        {
            DateTimeKind.Local       => dt.ToUniversalTime(),
            DateTimeKind.Utc         => dt,
            _ /* Unspecified */      => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
        };
        return DateTime.SpecifyKind(asUtc.Add(DubaiOffset), DateTimeKind.Unspecified);
    }

    /// <summary>Nullable variant — null in, null out.</summary>
    public static DateTime? ToDubai(this DateTime? dt) => dt?.ToDubai();

    /// <summary>
    /// Format a UTC DateTime in Dubai time. Default format mirrors what
    /// most views use ("yyyy-MM-dd HH:mm"). Pass a culture-invariant format
    /// — Razor escaping of the result is unchanged.
    /// </summary>
    public static string ToDubaiString(this DateTime dt, string format = "yyyy-MM-dd HH:mm")
        => dt.ToDubai().ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Nullable variant — returns <paramref name="emptyValue"/> when null.</summary>
    public static string ToDubaiString(this DateTime? dt, string format = "yyyy-MM-dd HH:mm", string emptyValue = "-")
        => dt.HasValue ? dt.Value.ToDubaiString(format) : emptyValue;
}
