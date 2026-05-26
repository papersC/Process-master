namespace ESEMS.Web.Services.Email;

/// <summary>
/// Minimal email interface used by the notification pipeline. Callers
/// should treat sending as best-effort — if SMTP is disabled or
/// misconfigured, the implementation logs and returns false but never
/// throws, so in-app notifications keep flowing regardless.
/// </summary>
public interface IEmailService
{
    /// <summary>True when SMTP config is present and the service will attempt delivery.</summary>
    bool IsEnabled { get; }

    /// <summary>Deliver an HTML email to a single recipient. Returns false on any failure.</summary>
    Task<bool> SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default);

    /// <summary>Validate SMTP by connecting + authenticating + sending a tiny self-test message to the from-address.</summary>
    Task<(bool ok, string message)> TestConnectionAsync(CancellationToken ct = default);
}
