using ESEMS.Web.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ESEMS.Tests.Integration;

/// <summary>
/// WebApplicationFactory override that:
///   1. Sets the "Testing" environment so Program.cs skips its raw-SQL bootstrap.
///   2. Swaps SqlServer DbContext registration for in-memory EF.
///   3. Registers TestAuthHandler as the default auth scheme so integration
///      tests can forge a caller identity via request headers.
/// </summary>
public sealed class EsemsTestFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "esems-test-" + Guid.NewGuid();

    // Pin the in-memory store to one shared root. Because the DbContext options
    // attach per-scope interceptor instances, EF can otherwise spin up a
    // separate internal service provider — and a separate store — per scope,
    // so data seeded through _factory.Services.CreateScope() would be invisible
    // to the request pipeline. A shared root keyed by _dbName guarantees every
    // context (seed scope + each HTTP request) sees the same data.
    private static readonly Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot _dbRoot = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove every EF-related registration that the production
            // UseSqlServer() call added — if we leave any of them behind,
            // EF sees two providers (SqlServer + InMemory) and aborts.
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(ApplicationDbContext) ||
                (d.ServiceType.FullName ?? "").StartsWith("Microsoft.EntityFrameworkCore") ||
                (d.ImplementationType?.FullName ?? "").StartsWith("Microsoft.EntityFrameworkCore"))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            // Register the in-memory DbContext WITH the same interceptors
            // production uses — without them AuditLog rows + Improvement
            // change-log rows aren't written and audit-trail tests fail.
            services.AddDbContext<ApplicationDbContext>((sp, opts) =>
            {
                opts.UseInMemoryDatabase(_dbName, _dbRoot);
                // The in-memory provider has no transactions; controllers that
                // wrap writes in BeginTransactionAsync (WorkflowController F-009,
                // SettingsHub.RevertImport F-024) would otherwise throw the
                // TransactionIgnoredWarning as an error under test. Downgrade it
                // to a no-op so those code paths run as they do in production.
                opts.ConfigureWarnings(w =>
                    w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
                opts.AddInterceptors(sp.GetRequiredService<ESEMS.Web.Services.Auditing.AuditSaveChangesInterceptor>());
                opts.AddInterceptors(sp.GetRequiredService<ESEMS.Web.Data.ImprovementChangeLogInterceptor>());
            });

            // Replace the cookie scheme with a forgeable test handler.
            services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // Bypass IAntiforgery in the test environment so integration
            // tests that POST to [ValidateAntiForgeryToken] endpoints don't
            // need to fetch + replay the token. The production behaviour
            // is unchanged — only this test factory swaps in the no-op.
            services.RemoveAll<IAntiforgery>();
            services.AddSingleton<IAntiforgery, NoOpAntiforgery>();

            // TestServer doesn't expose IConnectionItemsFeature on the
            // simulated connection. Program.cs registers .AddNegotiate() for
            // IIS Windows-auth, and NegotiateHandler runs as an
            // IAuthenticationRequestHandler on every request — it reads
            // Context.Features.Get<IConnectionItemsFeature>() and throws
            // NotSupportedException when it returns null. Result: every
            // WebApplicationFactory-based test 500'd before reaching its
            // controller. The IStartupFilter below prepends a tiny middleware
            // that attaches an empty IConnectionItemsFeature when one isn't
            // already present, so NegotiateHandler's null check passes and it
            // falls through to the next scheme. Production is unaffected
            // (Kestrel/IIS both supply the real feature).
            services.AddSingleton<IStartupFilter, ConnectionItemsStartupFilter>();
        });
    }

    /// <summary>
    /// Prepends a middleware that ensures every request has an
    /// IConnectionItemsFeature attached. See registration call site for the
    /// full rationale — short version: TestServer doesn't supply this
    /// feature, NegotiateHandler crashes without it, and a no-op stub gets
    /// the pipeline past the failure point with zero production impact.
    /// </summary>
    internal sealed class ConnectionItemsStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (ctx, n) =>
            {
                if (ctx.Features.Get<IConnectionItemsFeature>() == null)
                    ctx.Features.Set<IConnectionItemsFeature>(new StubConnectionItemsFeature());
                await n();
            });
            next(app);
        };
    }

    private sealed class StubConnectionItemsFeature : IConnectionItemsFeature
    {
        // NegotiateHandler reads connectionItems["AuthPersistence"] without
        // TryGetValue — it assumes the IDictionary returns null on miss the
        // way Kestrel's internal ConnectionItems class does. A plain
        // Dictionary<object,object?> throws KeyNotFoundException there, so we
        // wrap it in a null-safe shim. (Same contract Kestrel's ConnectionItems
        // implements; that class is internal so we can't reuse it directly.)
        public IDictionary<object, object?> Items { get; set; } = new NullSafeItems();
    }

    private sealed class NullSafeItems : IDictionary<object, object?>
    {
        private readonly Dictionary<object, object?> _inner = new();
        public object? this[object key]
        {
            get => _inner.TryGetValue(key, out var v) ? v : null;
            set => _inner[key] = value;
        }
        public ICollection<object> Keys => _inner.Keys;
        public ICollection<object?> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool IsReadOnly => false;
        public void Add(object key, object? value) => _inner.Add(key, value);
        public void Add(KeyValuePair<object, object?> item) => _inner.Add(item.Key, item.Value);
        public void Clear() => _inner.Clear();
        public bool Contains(KeyValuePair<object, object?> item) => _inner.ContainsKey(item.Key);
        public bool ContainsKey(object key) => _inner.ContainsKey(key);
        public void CopyTo(KeyValuePair<object, object?>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<object, object?>>)_inner).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<object, object?>> GetEnumerator() => _inner.GetEnumerator();
        public bool Remove(object key) => _inner.Remove(key);
        public bool Remove(KeyValuePair<object, object?> item) => _inner.Remove(item.Key);
        public bool TryGetValue(object key, out object? value) => _inner.TryGetValue(key, out value);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }

    /// <summary>
    /// Test-only IAntiforgery: returns "valid" by default so integration
    /// tests don't need to fetch + replay a CSRF token. A test that wants
    /// to verify the production CSRF behaviour (e.g. SecurityTests.Login_
    /// WithoutToken_IsRejected) opts INTO real validation by setting the
    /// `X-Test-Validate-Antiforgery: true` request header — in that case
    /// this no-op rejects the request like the real middleware would.
    /// Production code never sees this — it only resolves inside the test
    /// factory's service container.
    /// </summary>
    public const string ValidateAntiforgeryHeader = "X-Test-Validate-Antiforgery";

    private sealed class NoOpAntiforgery : IAntiforgery
    {
        private static readonly AntiforgeryTokenSet Empty = new(string.Empty, string.Empty, string.Empty, string.Empty);
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => Empty;
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => Empty;
        public Task<bool> IsRequestValidAsync(HttpContext httpContext)
            => Task.FromResult(!ShouldEnforce(httpContext));
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
        public Task ValidateRequestAsync(HttpContext httpContext)
        {
            if (ShouldEnforce(httpContext))
                throw new AntiforgeryValidationException("CSRF validation requested by test header.");
            return Task.CompletedTask;
        }

        private static bool ShouldEnforce(HttpContext ctx)
            => ctx.Request.Headers.TryGetValue(ValidateAntiforgeryHeader, out var v)
               && string.Equals(v.ToString(), "true", StringComparison.OrdinalIgnoreCase);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        return host;
    }
}
