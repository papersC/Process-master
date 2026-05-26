using System.Security.Cryptography;

namespace ESEMS.Web.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// HttpContext.Items key holding the per-request CSP nonce.
    /// Razor views read it via <c>Context.Items[CspNonceKey] as string</c>
    /// (or the helper <see cref="CspNonceExtensions.CspNonce"/>).
    /// </summary>
    public const string CspNonceKey = "CspNonce";

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Per-request nonce — base64 of 16 cryptographically-random bytes.
        // Stored on HttpContext.Items so Razor views (specifically the inline
        // <script> blocks in _Layout.cshtml / _LayoutNoSidebar.cshtml) can
        // read and emit it. CSP currently keeps 'unsafe-inline' for transition
        // safety: views that don't yet carry the nonce keep working. As views
        // migrate, 'unsafe-inline' can eventually be removed.
        var nonceBytes = new byte[16];
        RandomNumberGenerator.Fill(nonceBytes);
        var nonce = Convert.ToBase64String(nonceBytes);
        context.Items[CspNonceKey] = nonce;

        // OWASP recommended security headers.
        // M-8 (Tier-3): also strip the Server header as defense-in-depth, in
        // case Kestrel's AddServerHeader=false isn't enough (IIS in prod has
        // its own header path).
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Remove("Server");
            return Task.CompletedTask;
        });
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        // X-XSS-Protection is deprecated. Modern browsers ignore it; old
        // ones with the legacy auditor enabled can introduce side-channel
        // XSS when set to "1; mode=block". OWASP 2024+ recommends omitting
        // it OR setting to "0". We set "0" so a previously-cached value is
        // overridden.
        context.Response.Headers["X-XSS-Protection"] = "0";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        // Content Security Policy.
        //
        // M-7 (Tier-3) HOTFIX: previously split style-src into -elem (no
        // 'unsafe-inline', nonce-only) + -attr ('unsafe-inline'). That broke
        // every page because 8+ Razor partials emit raw <style> blocks
        // (e.g. _Layout's .page-header-logo size constraint) without a nonce
        // attribute — Chrome dropped their rules and the L1.png brand logo
        // rendered at native 6532×1263 instead of the 200×60 max. Until those
        // <style> blocks are migrated to carry nonce="@Context.CspNonce()",
        // style-src-elem keeps 'unsafe-inline' too. Result: same hardening
        // as before today's Tier-3 work (no regression vs. baseline) but
        // pending the nonce migration as deferred work.
        //
        // M-9 (Tier-3): the cdn.jsdelivr.net / cdnjs.cloudflare.com /
        // unpkg.com / cdn.datatables.net / cdn.tailwindcss.com entries are
        // removed from script-src. No view loads from these origins anymore
        // (everything is self-hosted under wwwroot/lib/). Google Fonts
        // remains in style-src and font-src — that's the only legit CDN
        // dependency left.
        // CSP-3 nuance: when both 'unsafe-inline' AND a nonce are present in
        // the SAME directive, browsers ignore 'unsafe-inline' and require the
        // nonce. That's the rule that broke things — we want 'unsafe-inline'
        // to ACTUALLY take effect for style-src-elem until nonce-migration
        // is done, so we drop the nonce from style-src-elem. (script-src
        // keeps the nonce because all <script> tags ARE nonce-tagged.)
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            $"script-src 'self' 'nonce-{nonce}' 'unsafe-eval'; " +
            "style-src-elem 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            "style-src-attr 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            "font-src 'self' data: https://fonts.gstatic.com; " +
            "img-src 'self' data: blob: https:; " +
            "connect-src 'self' ws: wss:; " +
            "frame-ancestors 'self'; " +
            "base-uri 'self'; " +
            "form-action 'self';";

        await _next(context);
    }
}

/// <summary>
/// Razor-view helper to read the per-request CSP nonce. Usage:
///   &lt;script nonce="@Context.CspNonce()"&gt;...&lt;/script&gt;
/// Returns empty string if no nonce was set (e.g. middleware not running).
/// </summary>
public static class CspNonceExtensions
{
    public static string CspNonce(this HttpContext context)
        => context.Items[SecurityHeadersMiddleware.CspNonceKey] as string ?? string.Empty;
}
