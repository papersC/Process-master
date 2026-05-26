using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;

namespace ESEMS.Web.Services.Email;

/// <summary>
/// Fallback defaults used when an AppSettings row is missing. The live
/// config comes from the database (Settings Hub → Email & Alerts).
/// </summary>
public class EmailOptions
{
    public bool Enabled { get; set; } = false;
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "noreply@local";
    public string FromName { get; set; } = "ESEMS";
    public int TimeoutSeconds { get; set; } = 15;
}

public class SmtpEmailService : IEmailService
{
    private readonly ApplicationDbContext _context;
    private readonly EmailOptions _fallback;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(ApplicationDbContext context, Microsoft.Extensions.Options.IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _context = context;
        _fallback = options.Value;
        _logger = logger;
    }

    public bool IsEnabled
    {
        get
        {
            var cfg = LoadAsync().GetAwaiter().GetResult();
            return cfg.Enabled && !string.IsNullOrWhiteSpace(cfg.Host);
        }
    }

    public async Task<bool> SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default)
    {
        var cfg = await LoadAsync(ct);
        if (!cfg.Enabled || string.IsNullOrWhiteSpace(cfg.Host))
        {
            _logger.LogInformation("Email disabled — skipping send to {To} subject={Subject}", toAddress, subject);
            return false;
        }
        if (string.IsNullOrWhiteSpace(toAddress))
        {
            _logger.LogDebug("No email address for recipient; skipping");
            return false;
        }

        try
        {
            using var client = BuildClient(cfg);
            using var msg = BuildMessage(cfg, toAddress, subject, htmlBody);
            await client.SendMailAsync(msg, ct);
            _logger.LogInformation("Email sent to {To} subject={Subject}", toAddress, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email send failed to {To} subject={Subject}", toAddress, subject);
            return false;
        }
    }

    public async Task<(bool ok, string message)> TestConnectionAsync(CancellationToken ct = default)
    {
        var cfg = await LoadAsync(ct);
        if (!cfg.Enabled) return (false, "Email is disabled in Settings Hub → Email & Alerts.");
        if (string.IsNullOrWhiteSpace(cfg.Host)) return (false, "SMTP Host is not configured.");
        if (string.IsNullOrWhiteSpace(cfg.FromAddress)) return (false, "From-address is not configured.");

        try
        {
            using var client = BuildClient(cfg);
            using var msg = BuildMessage(cfg, cfg.FromAddress,
                "ESEMS SMTP test",
                "<p>This is a self-test message from ESEMS. If you received it, your SMTP configuration is working.</p>");
            await client.SendMailAsync(msg, ct);
            return (true, $"Sent test message to {cfg.FromAddress} via {cfg.Host}:{cfg.Port}.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Load config live from AppSettings. SettingsHub Email tab writes keys
    /// with the Smtp.* prefix; anything missing falls back to appsettings.json.
    /// </summary>
    private async Task<EmailOptions> LoadAsync(CancellationToken ct = default)
    {
        var rows = await _context.AppSettings
            .Where(s => s.Key.StartsWith("Smtp."))
            .ToDictionaryAsync(s => s.Key, s => s.Value ?? "", ct);

        string Get(string k, string def = "") => rows.TryGetValue(k, out var v) && !string.IsNullOrEmpty(v) ? v : def;
        bool GetBool(string k, bool def) => rows.TryGetValue(k, out var v) ? v == "true" : def;
        int  GetInt (string k, int def) => rows.TryGetValue(k, out var v) && int.TryParse(v, out var n) ? n : def;

        return new EmailOptions
        {
            Enabled        = GetBool("Smtp.Enabled", _fallback.Enabled),
            Host           = Get("Smtp.Host", _fallback.Host),
            Port           = GetInt("Smtp.Port", _fallback.Port),
            EnableSsl      = GetBool("Smtp.EnableSsl", _fallback.EnableSsl),
            Username       = Get("Smtp.Username", _fallback.Username),
            Password       = Get("Smtp.Password", _fallback.Password),
            FromAddress    = Get("Smtp.FromEmail", _fallback.FromAddress),
            FromName       = Get("Smtp.FromName", _fallback.FromName),
            TimeoutSeconds = GetInt("Smtp.TimeoutSeconds", _fallback.TimeoutSeconds)
        };
    }

    private static SmtpClient BuildClient(EmailOptions cfg)
    {
        var client = new SmtpClient(cfg.Host, cfg.Port)
        {
            EnableSsl = cfg.EnableSsl,
            Timeout = cfg.TimeoutSeconds * 1000,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };
        if (!string.IsNullOrEmpty(cfg.Username))
            client.Credentials = new NetworkCredential(cfg.Username, cfg.Password);
        return client;
    }

    private static MailMessage BuildMessage(EmailOptions cfg, string to, string subject, string htmlBody)
    {
        var msg = new MailMessage
        {
            From = new MailAddress(cfg.FromAddress, cfg.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
            SubjectEncoding = System.Text.Encoding.UTF8,
            BodyEncoding = System.Text.Encoding.UTF8
        };
        msg.To.Add(new MailAddress(to));
        return msg;
    }
}
