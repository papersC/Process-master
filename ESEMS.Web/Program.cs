using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using ESEMS.Web;
using ESEMS.Web.Data;
using ESEMS.Web.Models;
using ESEMS.Web.Models.WorkloadAnalysis;
using ESEMS.Web.Security;
using ESEMS.Web.Helpers;
using ESEMS.Web.Services.AI;
using ESEMS.Web.Services.Bpmn;
using ESEMS.Web.Services.Analysis;
using ESEMS.Web.Services.Improvements;
using ESEMS.Web.Services.Common;
using ESEMS.Web.Services.Notifications;
using ESEMS.Web.Services.Workflow;
using ESEMS.Web.Services.Export;
using ESEMS.Web.Services.Integrations;
using ESEMS.Web.Hubs;
using System.Threading.RateLimiting;
using ESEMS.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Single config file: appsettings.json only (no appsettings.{Environment}.json overlay).
for (var i = builder.Configuration.Sources.Count - 1; i >= 0; i--)
{
    if (builder.Configuration.Sources[i] is JsonConfigurationSource json &&
        json.Path is { } path &&
        path.StartsWith("appsettings.", StringComparison.OrdinalIgnoreCase) &&
        !path.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase))
    {
        builder.Configuration.Sources.RemoveAt(i);
    }
}

// M-8 (Tier-3 audit): strip the `Server: Kestrel` header so the response
// doesn't fingerprint the platform for CVE-scanners. Under IIS in prod the
// IIS module owns the header — also strip it via web.config there.
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

// Add services to the container.

// Web encoder settings — by default, ASP.NET Core only treats BasicLatin as
// "safe" and encodes everything else (including Arabic) as &#xNNNN; entities.
// That renders fine inside HTML (browsers decode the entities), but when an
// Arabic string is dropped into a JS literal and assigned via .textContent,
// the browser treats the entity sequence as plain text and the user sees
// raw "&#x62C;..." on screen (notification badge bug). Allow all Unicode so
// bilingual strings survive JS-context round-trips unescaped.
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(options =>
{
    options.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All);
});

// Auditing (CUD via EF interceptor; optional reads via MVC filter)
builder.Services.AddScoped<ESEMS.Web.Services.Auditing.AuditSaveChangesInterceptor>();
builder.Services.AddScoped<ESEMS.Web.Services.Common.HierarchicalCodeService>();
builder.Services.AddScoped<ESEMS.Web.Filters.AuditViewFilter>();

// AI Service (Azure OpenAI) — uses IHttpClientFactory to prevent socket exhaustion
builder.Services.AddHttpClient("AzureOpenAI");
builder.Services.AddSingleton<IAIService, AzureOpenAIService>();

// External-system integrations (risk + process-performance) — provider/adapter pattern:
// views depend on IRiskProvider / IProcessPerformanceProvider; the concrete impl is
// picked at startup from appsettings:Integrations. Each external system's URL is per-
// tenant; tenants without the integration get NoOp* and the linked-data partials render
// nothing. See Services/Integrations/IntegrationServiceCollectionExtensions.cs.
//
// Settings-Hub-managed values from the AppSettings table win over appsettings.json:
// the bootstrap below patches them into the configuration tree BEFORE the providers'
// HttpClients are constructed, so admin saves apply on the next app-pool recycle.
IntegrationSettingsBootstrap.OverlayFromDatabase(builder.Configuration);
builder.Services.AddIntegrations(builder.Configuration);

// AWS Bedrock AI Service — registered separately for batch BPMN import
builder.Services.AddSingleton<AWSBedrockService>();

// Business logic services (extracted from controllers)
builder.Services.AddScoped<IBpmnProcessingService, BpmnProcessingService>();
builder.Services.AddSingleton<IBpmnValidator, BpmnValidator>();
builder.Services.AddSingleton<IBpmnPostProcessor, BpmnPostProcessor>();
builder.Services.AddScoped<IBpmnLaneReconciler, BpmnLaneReconciler>();
builder.Services.AddScoped<IBpmnLibraryImporter, BpmnLibraryImporter>();
builder.Services.AddSingleton<IVisioExtractor, VisioExtractor>();
builder.Services.AddScoped<IProcessAnalysisService, ProcessAnalysisService>();
builder.Services.AddScoped<IImprovementWorkflowService, ImprovementWorkflowService>();
builder.Services.AddScoped<IMeasurementCollectionService, MeasurementCollectionService>();
builder.Services.AddHostedService<MeasurementReminderHostedService>();
// Audit #1: post-closure benefits realisation scheduler.
builder.Services.AddHostedService<ESEMS.Web.Services.Improvements.BenefitsRealizationScheduler>();
// Audit #9: per-initiative change-log interceptor.
builder.Services.AddSingleton<ESEMS.Web.Data.ImprovementChangeLogInterceptor>();
// Audit #12: time-series benefits aggregator.
builder.Services.AddScoped<ESEMS.Web.Services.Improvements.BenefitsAggregationService>();
// Audit #5: stall-detection escalation service.
builder.Services.Configure<ESEMS.Web.Services.Improvements.StallDetectionOptions>(
    builder.Configuration.GetSection(ESEMS.Web.Services.Improvements.StallDetectionOptions.SectionName));
builder.Services.AddHostedService<ESEMS.Web.Services.Improvements.InitiativeStallDetectionService>();
// Audit #16: recurring stage-gate review scheduler.
builder.Services.AddHostedService<ESEMS.Web.Services.Improvements.RecurringReviewScheduler>();
builder.Services.AddScoped<IEntityNumberGenerator, EntityNumberGenerator>();

// Notification services (SignalR + in-app)
builder.Services.AddSignalR();
builder.Services.AddScoped<IRealtimeNotifier, SignalRNotifier>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Email (SMTP). Reads config from the "Email" section; when Enabled is
// false the service is a logged no-op so dev environments don't need real
// SMTP creds.
builder.Services.Configure<ESEMS.Web.Services.Email.EmailOptions>(
    builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<ESEMS.Web.Services.Email.IEmailService, ESEMS.Web.Services.Email.SmtpEmailService>();

// Workflow engine (approval workflow)
builder.Services.AddScoped<IWorkflowService, ESEMS.Web.Services.Workflow.WorkflowService>();
// Background worker that enforces SLA auto-escalation and expired-delegation
// revert. Scans pending workflow steps every 15 minutes.
builder.Services.AddHostedService<ESEMS.Web.Services.Workflow.ApprovalSlaHostedService>();

// Export service (unified Excel/PDF/CSV)
builder.Services.AddScoped<IExportService, ESEMS.Web.Services.Export.ExportService>();

// Excel import service (Administrator Hub → Data tab uploads)
builder.Services.AddScoped<ESEMS.Web.Services.Import.IExcelImportService, ESEMS.Web.Services.Import.ExcelImportService>();

// AI Assistant (RAG pipeline)
builder.Services.AddSingleton<ESEMS.Web.Services.AI.VectorStoreService>();
builder.Services.AddScoped<ESEMS.Web.Services.AI.AiAssistantService>();
builder.Services.AddHostedService<ESEMS.Web.Services.AI.VectorSyncBackgroundService>();

// Database Context
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    var sqlConnection = SqlConnectionStringHelper.PreferTcp(
        builder.Configuration.GetConnectionString("DefaultConnection"));
    options.UseSqlServer(sqlConnection, sql =>
        sql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null));
    options.AddInterceptors(sp.GetRequiredService<ESEMS.Web.Services.Auditing.AuditSaveChangesInterceptor>());
    options.AddInterceptors(sp.GetRequiredService<ESEMS.Web.Data.ImprovementChangeLogInterceptor>());
    // Suppress pending model changes warning - new tables are auto-created via raw SQL
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

// Identity Configuration - DISABLED: Using custom user table instead of ASP.NET Identity
// builder.Services.AddIdentity<User, IdentityRole>(options =>
// {
//     options.Password.RequireDigit = true;
//     options.Password.RequireLowercase = true;
//     options.Password.RequireUppercase = true;
//     options.Password.RequireNonAlphanumeric = false;
//     options.Password.RequiredLength = 8;
//     options.User.RequireUniqueEmail = true;
//     options.SignIn.RequireConfirmedEmail = false;
// })
// .AddEntityFrameworkStores<ApplicationDbContext>()
// .AddDefaultTokenProviders();

// Cookie sign-in + Negotiate challenge for "Sign in with Windows" on IIS.
static string AccountRoute(IConfiguration config, string action)
{
    var pathBase = config["AppSettings:PathBase"];
    var prefix = string.IsNullOrEmpty(pathBase) || pathBase == "/"
        ? string.Empty
        : pathBase.TrimEnd('/');
    return $"{prefix}/Account/{action}";
}

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "Cookies";
        options.DefaultChallengeScheme = NegotiateDefaults.AuthenticationScheme;
    })
    .AddNegotiate()
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = AccountRoute(builder.Configuration, "Login");
        options.AccessDeniedPath = AccountRoute(builder.Configuration, "AccessDenied");
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // In production, ALWAYS send cookies over HTTPS to prevent
        // session hijacking via MITM. In development we need HTTP on
        // localhost, so SameAsRequest is the dev-safe fallback.
        options.Cookie.SecurePolicy = builder.Environment.IsProduction()
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;

        // FU-002: validate the user's security stamp on each (interval-gated)
        // request. A password change bumps the stamp (AccountController.
        // ChangePassword), so every OTHER already-issued cookie — carrying the
        // old stamp — is rejected and signed out. Also revokes existing
        // sessions of users who get deactivated/deleted. A null DB stamp and a
        // missing/empty claim are treated as equal, so this rolls out without
        // logging everyone out on deploy.
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async ctx =>
            {
                // Re-check at most every 30 min to bound DB load (mirrors
                // ASP.NET Identity's SecurityStampValidator interval).
                var lastStr = ctx.Properties.GetString("ss_checked");
                if (lastStr != null
                    && DateTimeOffset.TryParse(lastStr, null, DateTimeStyles.RoundtripKind, out var last)
                    && DateTimeOffset.UtcNow - last < TimeSpan.FromMinutes(30))
                    return;

                var idStr = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(idStr, out var uid)) return; // not our cookie shape

                var db = ctx.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var row = await db.CustomUsers.AsNoTracking()
                    .Where(u => u.UserId == uid)
                    .Select(u => new { u.SecurityStamp, u.IsActive })
                    .FirstOrDefaultAsync();

                var claimStamp = ctx.Principal?.FindFirst("SecurityStamp")?.Value ?? string.Empty;
                if (row == null || !row.IsActive || (row.SecurityStamp ?? string.Empty) != claimStamp)
                {
                    ctx.RejectPrincipal();
                    await ctx.HttpContext.SignOutAsync("Cookies");
                    return;
                }

                ctx.Properties.SetString("ss_checked", DateTimeOffset.UtcNow.ToString("o"));
                ctx.ShouldRenew = true;
            }
        };
    });

// Plan X: matrix-driven authorization handler. Resolves
// [Authorize(Policy = "Module.Action")] attributes by checking the
// user's Permission claims, which are emitted at login from their
// UserRoleGroup assignments.
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Authorization / RBAC
builder.Services.AddAuthorization(options =>
{
    // Require authentication by default across the app.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // ---- Broad policies: resolved purely from Plan X Permission claims ----
    // These five policy names are referenced by the 200+ existing
    // [Authorize(Policy = AppPolicies.CanXxx)] attributes scattered across
    // controllers. Earlier they OR-ed Permission claims against legacy
    // ClaimTypes.Role values to keep pre-migration sessions working; the
    // legacy CustomRole/CustomUserRole system has been removed, so every
    // signed-in user now carries Permission claims sourced from their
    // UserRoleGroup assignments and these policies match against those alone.

    // Any authenticated user is allowed to View.
    options.AddPolicy(AppPolicies.CanView, policy =>
        policy.RequireAuthenticatedUser());

    // CanEdit: any `.Edit`/`.Create`/`.Delete` permission claim, or the *.* wildcard.
    options.AddPolicy(AppPolicies.CanEdit, policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("Permission").Any(c =>
                c.Value == "*.*" || c.Value.EndsWith(".Edit") ||
                c.Value.EndsWith(".Create") || c.Value.EndsWith(".Delete"))));

    // CanDelete: `.Delete` permission claim, or *.*.
    options.AddPolicy(AppPolicies.CanDelete, policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("Permission").Any(c =>
                c.Value == "*.*" || c.Value.EndsWith(".Delete"))));

    // CanApprove: `.Approve` permission claim, or *.*.
    options.AddPolicy(AppPolicies.CanApprove, policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("Permission").Any(c =>
                c.Value == "*.*" || c.Value.EndsWith(".Approve"))));

    // CanAdmin: the *.* permission claim only — granted by the Administrator
    // RoleGroup (seeded with Permissions = '*.*').
    options.AddPolicy(AppPolicies.CanAdmin, policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("Permission").Any(c => c.Value == "*.*")));

    // ---- Plan X: register one granular policy per Module.Action ----
    // Controllers can now use e.g. [Authorize(Policy = "Improvement.Edit")].
    foreach (var permission in AppPolicies.AllModuleActions)
    {
        options.AddPolicy(permission, policy =>
            policy.AddRequirements(new PermissionRequirement(permission)));
    }
});

// Session Configuration
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Localization Configuration (Arabic and English)
builder.Services.AddLocalization();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en"),
        new CultureInfo("ar")
    };

    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // Use cookie to store user's language preference
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});

// SEC-007: validate the anti-forgery token sent as a request HEADER by AJAX
// (the global fetch/jQuery injector in _Layout sends "RequestVerificationToken").
// Standard form posts still validate via the hidden __RequestVerificationToken.
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

// MVC with Razor Runtime Compilation
builder.Services.AddControllersWithViews(options =>
    {
        // Controlled by configuration: Auditing:LogReads
        options.Filters.AddService<ESEMS.Web.Filters.AuditViewFilter>();

        // SEC-007: enforce anti-forgery on every unsafe (POST/PUT/DELETE/PATCH)
        // action by default, so a new POST is protected even if a developer
        // forgets [ValidateAntiForgeryToken]. Browser AJAX sends the token via
        // the global fetch/jQuery injector in _Layout (HeaderName configured
        // above); the MySpace XHR upload already sends it; standard form posts
        // include the hidden field. GET/HEAD/OPTIONS/TRACE and
        // [IgnoreAntiforgeryToken] endpoints (BaseController.ChangeLanguage) are
        // exempt automatically. No external/non-browser POST endpoints exist.
        options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    })
    .AddViewLocalization()
    // Route DataAnnotations validation messages through the shared resource
    // bundle so [Required], [StringLength], [Range] etc. respect the current
    // UI culture without each model needing its own .resx companion file.
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(SharedResource));
    })
    .AddRazorRuntimeCompilation();

// LocalizedValidationAttributeAdapterProvider intercepts attributes that have
// no explicit ErrorMessage (the common case) and injects the default template
// as the message — that template is then the resource key the localizer looks
// up. Without this, default [Required]/[StringLength]/[Range] messages stay
// in English no matter what AddDataAnnotationsLocalization is configured with.
builder.Services.AddSingleton<
    Microsoft.AspNetCore.Mvc.DataAnnotations.IValidationAttributeAdapterProvider,
    ESEMS.Web.Services.Common.LocalizedValidationAttributeAdapterProvider>();

// Response compression — BRotli + Gzip on HTTPS. Exports (Excel, PDF),
// BPMN XML, and the large SettingsHub pages all compress 5-10x. The
// default MIME list covers text/html, application/json, text/css, text/js;
// we add application/xml for the BPMN endpoints and application/pdf is
// intentionally NOT added (PDFs are already compressed internally).
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes
        .Concat(new[]
        {
            "application/xml",
            "application/bpmn+xml",
            "image/svg+xml",
            "application/manifest+json"
        });
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(
    opts => opts.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(
    opts => opts.Level = System.IO.Compression.CompressionLevel.Fastest);

// HTTP Context Accessor
builder.Services.AddHttpContextAccessor();

// In-memory cache for rate-limiting + permission lookups. For a
// multi-instance deployment behind a load balancer, swap to
// IDistributedCache (Redis/SQL) to get cross-instance consistency.
builder.Services.AddMemoryCache();

// Data-scoping service — resolves the current user's ScopeLevel +
// visible org-unit tree from their claims. Scoped because it reads
// User.Claims which are per-request.
builder.Services.AddScoped<ESEMS.Web.Services.Common.IScopingService, ESEMS.Web.Services.Common.ScopingService>();

// Rate Limiting (OWASP protection). Production keeps the bounded fixed-window
// limit; Development exempts loopback so interactive testing — including
// browser-driven QA agents that load many assets per page — doesn't trip the
// limit and get spurious 429s that look like an "app outage" but are just
// throttling. The seam is per-environment so production behaviour is unchanged.
builder.Services.AddRateLimiter(options =>
{
    var isDevelopment = builder.Environment.IsDevelopment();

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Exempt SignalR, health, login, and static files from rate limiting
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/layouts", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetNoLimiter("exempt");
        }

        // Development: bypass the limiter entirely for loopback connections so
        // local interactive testing and the Chrome-extension QA agent can run
        // unconstrained. Remote IPs (LAN devices etc.) still get the limit.
        if (isDevelopment)
        {
            var ip = context.Connection.RemoteIpAddress;
            if (ip != null && System.Net.IPAddress.IsLoopback(ip))
                return RateLimitPartition.GetNoLimiter("dev-loopback");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 50,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Background-service isolation. .NET 8+ defaults BackgroundServiceExceptionBehavior
// to StopHost — an unhandled exception in ANY IHostedService (we run 6:
// ApprovalSlaHostedService, BenefitsRealizationScheduler, InitiativeStall-
// DetectionService, RecurringReviewScheduler, VectorSyncBackgroundService,
// MeasurementReminderHostedService) brings the whole web app down. Switching to
// Ignore keeps the host alive and lets the standard logger record the failure;
// each service is independently responsible for its own retry/recovery loop.
builder.Services.Configure<HostOptions>(opts =>
    opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database");

var app = builder.Build();

// Apply database migrations. Skipped under the "Testing" environment so
// WebApplicationFactory-driven integration tests can run against an
// in-memory DbContext provider without our SqlServer-specific bootstrap.
if (!app.Environment.IsEnvironment("Testing"))
{
    await DatabaseStartupRunner.RunWithDatabaseAsync(app, async (context, logger, ct) =>
    {
    await DatabaseStartupRunner.ApplyMigrationsPipelineAsync(context, logger, ct);

    // Ensure ProcessServices table exists (many-to-many between Process and Service)
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProcessServices')
        BEGIN
            CREATE TABLE [dbo].[ProcessServices] (
                [ProcessId] NVARCHAR(450) NOT NULL,
                [ServiceId] NVARCHAR(450) NOT NULL,
                [Criticality] INT NOT NULL DEFAULT 3,
                [IsMandatory] BIT NOT NULL DEFAULT 1,
                [Notes] NVARCHAR(MAX) NULL,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [CreatedById] NVARCHAR(MAX) NULL,
                [UpdatedById] NVARCHAR(MAX) NULL,
                [IsActive] BIT NOT NULL DEFAULT 1,
                CONSTRAINT [PK_ProcessServices] PRIMARY KEY ([ProcessId], [ServiceId]),
                CONSTRAINT [FK_ProcessServices_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [dbo].[Processes] ([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_ProcessServices_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [dbo].[Services] ([Id]) ON DELETE CASCADE
            );
            CREATE INDEX [IX_ProcessServices_ServiceId] ON [dbo].[ProcessServices] ([ServiceId]);
        END
    ");

    // Ensure ProcessStrategicObjectives table exists (many-to-many between Process and StrategicObjective)
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProcessStrategicObjectives')
        BEGIN
            CREATE TABLE [dbo].[ProcessStrategicObjectives] (
                [ProcessId] NVARCHAR(450) NOT NULL,
                [StrategicObjectiveId] NVARCHAR(450) NOT NULL,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [CreatedById] NVARCHAR(MAX) NULL,
                [IsActive] BIT NOT NULL DEFAULT 1,
                CONSTRAINT [PK_ProcessStrategicObjectives] PRIMARY KEY ([ProcessId], [StrategicObjectiveId]),
                CONSTRAINT [FK_ProcessStrategicObjectives_Processes] FOREIGN KEY ([ProcessId]) REFERENCES [dbo].[Processes] ([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_ProcessStrategicObjectives_StrategicObjectives] FOREIGN KEY ([StrategicObjectiveId]) REFERENCES [dbo].[StrategicObjectives] ([Id]) ON DELETE CASCADE
            );
            CREATE INDEX [IX_ProcessStrategicObjectives_StrategicObjectiveId] ON [dbo].[ProcessStrategicObjectives] ([StrategicObjectiveId]);
        END
    ");

    // Ensure UserDocuments table exists (per-user "My Space" document library)
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserDocuments')
        BEGIN
            CREATE TABLE [dbo].[UserDocuments] (
                [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                [UserId] INT NOT NULL,
                [FileName] NVARCHAR(500) NOT NULL,
                [OriginalName] NVARCHAR(500) NOT NULL,
                [ContentType] NVARCHAR(200) NOT NULL,
                [FileSize] BIGINT NOT NULL DEFAULT 0,
                [Description] NVARCHAR(1000) NULL,
                [Tags] NVARCHAR(500) NULL,
                [Category] NVARCHAR(100) NOT NULL DEFAULT 'General',
                [UploadedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [IsDeleted] BIT NOT NULL DEFAULT 0
            );
            CREATE INDEX [IX_UserDocuments_UserId] ON [dbo].[UserDocuments] ([UserId]);
            CREATE INDEX [IX_UserDocuments_IsDeleted] ON [dbo].[UserDocuments] ([IsDeleted]);
        END
    ");

    // Ensure ProcessDocuments table exists (links Process → UserDocument with metadata)
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProcessDocuments')
        BEGIN
            CREATE TABLE [dbo].[ProcessDocuments] (
                [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                [ProcessId] NVARCHAR(450) NOT NULL,
                [UserDocumentId] NVARCHAR(450) NOT NULL,
                [DocumentCategoryId] NVARCHAR(450) NULL,
                [DocumentTypeId] NVARCHAR(450) NULL,
                [DocumentLanguage] NVARCHAR(50) NULL,
                [DisplayOrder] INT NOT NULL DEFAULT 0,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [CreatedById] NVARCHAR(300) NULL,
                CONSTRAINT [FK_ProcessDocuments_Processes] FOREIGN KEY ([ProcessId]) REFERENCES [dbo].[Processes] ([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_ProcessDocuments_UserDocuments] FOREIGN KEY ([UserDocumentId]) REFERENCES [dbo].[UserDocuments] ([Id])
            );
            CREATE INDEX [IX_ProcessDocuments_ProcessId] ON [dbo].[ProcessDocuments] ([ProcessId]);
            CREATE INDEX [IX_ProcessDocuments_UserDocumentId] ON [dbo].[ProcessDocuments] ([UserDocumentId]);
        END
    ");

    // Ensure ServiceStrategicObjectives table exists (many-to-many between Service and StrategicObjective)
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ServiceStrategicObjectives')
        BEGIN
            CREATE TABLE [dbo].[ServiceStrategicObjectives] (
                [ServiceId] NVARCHAR(450) NOT NULL,
                [StrategicObjectiveId] NVARCHAR(450) NOT NULL,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [CreatedById] NVARCHAR(MAX) NULL,
                [IsActive] BIT NOT NULL DEFAULT 1,
                CONSTRAINT [PK_ServiceStrategicObjectives] PRIMARY KEY ([ServiceId], [StrategicObjectiveId]),
                CONSTRAINT [FK_ServiceStrategicObjectives_Services] FOREIGN KEY ([ServiceId]) REFERENCES [dbo].[Services] ([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_ServiceStrategicObjectives_StrategicObjectives] FOREIGN KEY ([StrategicObjectiveId]) REFERENCES [dbo].[StrategicObjectives] ([Id]) ON DELETE CASCADE
            );
            CREATE INDEX [IX_ServiceStrategicObjectives_StrategicObjectiveId] ON [dbo].[ServiceStrategicObjectives] ([StrategicObjectiveId]);
        END
    ");

    // Create Notifications table
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Notifications')
        BEGIN
            CREATE TABLE [dbo].[Notifications] (
                [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                [UserId] INT NULL,
                [TitleEn] NVARCHAR(MAX) NOT NULL DEFAULT '',
                [TitleAr] NVARCHAR(MAX) NOT NULL DEFAULT '',
                [MessageEn] NVARCHAR(MAX) NOT NULL DEFAULT '',
                [MessageAr] NVARCHAR(MAX) NOT NULL DEFAULT '',
                [Type] NVARCHAR(50) NOT NULL DEFAULT 'Info',
                [IsRead] BIT NOT NULL DEFAULT 0,
                [ReadAt] DATETIME2 NULL,
                [RelatedEntityId] NVARCHAR(450) NULL,
                [RelatedEntityType] NVARCHAR(100) NULL,
                [ActionUrl] NVARCHAR(MAX) NULL,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
            );
            CREATE INDEX [IX_Notifications_UserId] ON [dbo].[Notifications] ([UserId]);
        END
    ");

    // Create NotificationPreferences table
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NotificationPreferences')
        BEGIN
            CREATE TABLE [dbo].[NotificationPreferences] (
                [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                [UserId] INT NOT NULL,
                [EnableInApp] BIT NOT NULL DEFAULT 1,
                [EnableEmail] BIT NOT NULL DEFAULT 1
            );
        END
    ");

    // Add multi-rule columns on top of the existing ApprovalConfigurations
    // table. These are the condition bands that let the admin route
    // approvals by cost, impact, horizon, innovation type, and duration.
    // Each ALTER is guarded so they only run once.
    await context.Database.ExecuteSqlRawAsync(@"
        IF COL_LENGTH('dbo.ApprovalConfigurations', 'RuleName') IS NULL
            ALTER TABLE [dbo].[ApprovalConfigurations] ADD [RuleName] NVARCHAR(200) NULL;
        IF COL_LENGTH('dbo.ApprovalConfigurations', 'Priority') IS NULL
            ALTER TABLE [dbo].[ApprovalConfigurations] ADD [Priority] INT NOT NULL DEFAULT 100;
        IF COL_LENGTH('dbo.ApprovalConfigurations', 'MinCostSavings') IS NULL
            ALTER TABLE [dbo].[ApprovalConfigurations] ADD [MinCostSavings] DECIMAL(18,2) NULL;
        IF COL_LENGTH('dbo.ApprovalConfigurations', 'MaxCostSavings') IS NULL
            ALTER TABLE [dbo].[ApprovalConfigurations] ADD [MaxCostSavings] DECIMAL(18,2) NULL;
        IF COL_LENGTH('dbo.ApprovalConfigurations', 'MinImpactScore') IS NULL
            ALTER TABLE [dbo].[ApprovalConfigurations] ADD [MinImpactScore] INT NULL;
        IF COL_LENGTH('dbo.ApprovalConfigurations', 'MaxImpactScore') IS NULL
            ALTER TABLE [dbo].[ApprovalConfigurations] ADD [MaxImpactScore] INT NULL;
        IF COL_LENGTH('dbo.ApprovalConfigurations', 'MinDurationDays') IS NULL
            ALTER TABLE [dbo].[ApprovalConfigurations] ADD [MinDurationDays] INT NULL;
        IF COL_LENGTH('dbo.ApprovalConfigurations', 'MaxDurationDays') IS NULL
            ALTER TABLE [dbo].[ApprovalConfigurations] ADD [MaxDurationDays] INT NULL;
        IF COL_LENGTH('dbo.ApprovalConfigurations', 'Horizon') IS NULL
            ALTER TABLE [dbo].[ApprovalConfigurations] ADD [Horizon] NVARCHAR(50) NULL;
        IF COL_LENGTH('dbo.ApprovalConfigurations', 'InnovationType') IS NULL
            ALTER TABLE [dbo].[ApprovalConfigurations] ADD [InnovationType] NVARCHAR(50) NULL;
    ");

    // Stamp the chosen rule on the workflow instance so ProcessActionAsync
    // can walk to the *same rule's* Level 2 approver when there are many.
    await context.Database.ExecuteSqlRawAsync(@"
        IF COL_LENGTH('dbo.WorkflowInstances', 'ApprovalConfigurationId') IS NULL
            ALTER TABLE [dbo].[WorkflowInstances] ADD [ApprovalConfigurationId] NVARCHAR(450) NULL;
    ");

    // Settings Hub — generic key/value app settings table (backs General,
    // Email & Alerts, Integrations, Data Import preferences tabs).
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AppSettings')
        BEGIN
            CREATE TABLE [dbo].[AppSettings] (
                [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                [Key] NVARCHAR(200) NOT NULL DEFAULT '',
                [Value] NVARCHAR(MAX) NULL,
                [Category] NVARCHAR(100) NOT NULL DEFAULT 'General',
                [DataType] NVARCHAR(50) NOT NULL DEFAULT 'string',
                [DescriptionEn] NVARCHAR(500) NULL,
                [DescriptionAr] NVARCHAR(500) NULL,
                [Hidden] BIT NOT NULL DEFAULT 0,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
            );
            CREATE UNIQUE INDEX [UX_AppSettings_Key] ON [dbo].[AppSettings] ([Key]);
        END
    ");

    // Seed a few demo settings so the General tab is never empty
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM [dbo].[AppSettings] WHERE [Key] = 'Organization.Name')
        BEGIN
            INSERT INTO [dbo].[AppSettings] ([Id], [Key], [Value], [Category], [DataType], [DescriptionEn], [DescriptionAr])
            VALUES
                (NEWID(), N'Organization.Name', N'MBRHE', N'General', N'string', N'Organization short name displayed in the top bar', N'اسم المؤسسة المختصر الظاهر في الشريط العلوي'),
                (NEWID(), N'Organization.LogoUrl', N'/L1.png', N'General', N'string', N'Path to the organization logo', N'مسار شعار المؤسسة'),
                (NEWID(), N'System.DefaultLanguage', N'en', N'General', N'string', N'Default UI language on first visit', N'لغة الواجهة الافتراضية'),
                (NEWID(), N'System.SessionTimeoutMinutes', N'60', N'General', N'int', N'Session timeout in minutes', N'مدة الجلسة بالدقائق'),
                (NEWID(), N'System.MaxUploadMb', N'25', N'General', N'int', N'Max upload size in MB', N'أقصى حجم للرفع بالميجابايت');
        END
    ");

    // Initiative Stall Detection thresholds — pulled from AppSettings each
    // poll, so admins can retune without redeploying. Three rows under one
    // collapsible category in the General tab. Defaults match the historical
    // class constants (14 / 30 / 60 days).
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM [dbo].[AppSettings] WHERE [Key] = 'StallDetection.AmberDays')
        BEGIN
            INSERT INTO [dbo].[AppSettings] ([Id], [Key], [Value], [Category], [DataType], [DescriptionEn], [DescriptionAr])
            VALUES
                (NEWID(), N'StallDetection.AmberDays',    N'14', N'Initiative Stall Detection', N'int',
                    N'Days of inactivity before an Amber alert pings the initiative owner.',
                    N'عدد أيام عدم النشاط قبل تنبيه أصفر يُرسل إلى مالك المبادرة.'),
                (NEWID(), N'StallDetection.RedDays',      N'30', N'Initiative Stall Detection', N'int',
                    N'Days of inactivity before a Red alert pings owner + unit head.',
                    N'عدد أيام عدم النشاط قبل تنبيه أحمر يُرسل إلى المالك ورئيس الوحدة.'),
                (NEWID(), N'StallDetection.CriticalDays', N'60', N'Initiative Stall Detection', N'int',
                    N'Days of inactivity before a Critical alert pings owner + unit head + admins.',
                    N'عدد أيام عدم النشاط قبل تنبيه حرج يُرسل إلى المالك ورئيس الوحدة والمشرفين.');
        END
    ");

    // Repair AppSetting Arabic descriptions that an earlier non-Unicode seed
    // path stored as literal '?' (e.g. INSERT without the N'' prefix, or a
    // varchar round-trip). The guarded INSERTs above won't overwrite existing
    // rows, so these stay corrupted on any DB seeded by that build. Idempotent
    // and self-correcting: only rows whose DescriptionAr still contains '?'
    // are touched, so correct values (and any later edits) are never clobbered.
    await context.Database.ExecuteSqlRawAsync(@"
        UPDATE [dbo].[AppSettings] SET [DescriptionAr] = N'اسم المؤسسة المختصر الظاهر في الشريط العلوي' WHERE [Key] = 'Organization.Name'             AND [DescriptionAr] LIKE N'%?%';
        UPDATE [dbo].[AppSettings] SET [DescriptionAr] = N'مسار شعار المؤسسة'                          WHERE [Key] = 'Organization.LogoUrl'          AND [DescriptionAr] LIKE N'%?%';
        UPDATE [dbo].[AppSettings] SET [DescriptionAr] = N'لغة الواجهة الافتراضية'                     WHERE [Key] = 'System.DefaultLanguage'        AND [DescriptionAr] LIKE N'%?%';
        UPDATE [dbo].[AppSettings] SET [DescriptionAr] = N'مدة الجلسة بالدقائق'                        WHERE [Key] = 'System.SessionTimeoutMinutes'  AND [DescriptionAr] LIKE N'%?%';
        UPDATE [dbo].[AppSettings] SET [DescriptionAr] = N'أقصى حجم للرفع بالميجابايت'                 WHERE [Key] = 'System.MaxUploadMb'            AND [DescriptionAr] LIKE N'%?%';
        UPDATE [dbo].[AppSettings] SET [DescriptionAr] = N'عدد أيام عدم النشاط قبل تنبيه أصفر يُرسل إلى مالك المبادرة.'                 WHERE [Key] = 'StallDetection.AmberDays'    AND [DescriptionAr] LIKE N'%?%';
        UPDATE [dbo].[AppSettings] SET [DescriptionAr] = N'عدد أيام عدم النشاط قبل تنبيه أحمر يُرسل إلى المالك ورئيس الوحدة.'           WHERE [Key] = 'StallDetection.RedDays'      AND [DescriptionAr] LIKE N'%?%';
        UPDATE [dbo].[AppSettings] SET [DescriptionAr] = N'عدد أيام عدم النشاط قبل تنبيه حرج يُرسل إلى المالك ورئيس الوحدة والمشرفين.'  WHERE [Key] = 'StallDetection.CriticalDays' AND [DescriptionAr] LIKE N'%?%';
    ");

    // Settings Hub — Role Groups table
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RoleGroups')
        BEGIN
            CREATE TABLE [dbo].[RoleGroups] (
                [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                [NameEn] NVARCHAR(200) NOT NULL DEFAULT '',
                [NameAr] NVARCHAR(200) NOT NULL DEFAULT '',
                [DescriptionEn] NVARCHAR(500) NULL,
                [DescriptionAr] NVARCHAR(500) NULL,
                [ScopeLevel] NVARCHAR(50) NOT NULL DEFAULT 'All',
                [Permissions] NVARCHAR(MAX) NULL,
                [Icon] NVARCHAR(100) NOT NULL DEFAULT 'users',
                [Color] NVARCHAR(20) NOT NULL DEFAULT '#005B99',
                [IsActive] BIT NOT NULL DEFAULT 1,
                [MemberCount] INT NOT NULL DEFAULT 0,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
            );
        END
    ");

    // Add IsSystemRole + Code columns to RoleGroups (idempotent)
    await context.Database.ExecuteSqlRawAsync(@"
        IF COL_LENGTH('dbo.RoleGroups', 'IsSystemRole') IS NULL
            ALTER TABLE [dbo].[RoleGroups] ADD [IsSystemRole] BIT NOT NULL DEFAULT 0;
        IF COL_LENGTH('dbo.RoleGroups', 'Code') IS NULL
            ALTER TABLE [dbo].[RoleGroups] ADD [Code] NVARCHAR(100) NULL;
    ");

    // Mark the seeded starter groups as system roles so they can't be deleted.
    await context.Database.ExecuteSqlRawAsync(@"
        UPDATE [dbo].[RoleGroups] SET [IsSystemRole] = 1, [Code] = 'quality-officer'
          WHERE [NameEn] = 'Quality Officer' AND [IsSystemRole] = 0;
        UPDATE [dbo].[RoleGroups] SET [IsSystemRole] = 1, [Code] = 'process-owner'
          WHERE [NameEn] = 'Process Owner' AND [IsSystemRole] = 0;
        UPDATE [dbo].[RoleGroups] SET [IsSystemRole] = 1, [Code] = 'improvement-analyst'
          WHERE [NameEn] = 'Improvement Analyst' AND [IsSystemRole] = 0;
        UPDATE [dbo].[RoleGroups] SET [IsSystemRole] = 1, [Code] = 'risk-manager'
          WHERE [NameEn] = 'Risk Manager' AND [IsSystemRole] = 0;
    ");

    // DYN-001 (root cause): the role-group INSERT below is gated by IF NOT EXISTS,
    // so on any DB seeded by an earlier build the four system groups keep their
    // STALE [Permissions] (e.g. missing Process.View, or the obsolete
    // 'Measurement.Read' typo), which silently leaves every role-group-only user
    // with no usable permissions. Refresh the canonical permission set for the
    // system groups on every boot — idempotent, and scoped to [IsSystemRole]=1 so
    // any user-created or admin-customized groups are never touched.
    await context.Database.ExecuteSqlRawAsync(@"
        UPDATE [dbo].[RoleGroups] SET [Permissions]=N'Improvement.View,Improvement.Approve,Improvement.Edit,Process.View,Service.View,Reports.View,Reports.Export'
          WHERE [Code]='quality-officer' AND [IsSystemRole]=1;
        UPDATE [dbo].[RoleGroups] SET [Permissions]=N'Process.View,Process.Edit,Improvement.View,Improvement.Create,Improvement.Edit,Measurement.View'
          WHERE [Code]='process-owner' AND [IsSystemRole]=1;
        UPDATE [dbo].[RoleGroups] SET [Permissions]=N'Improvement.View,Improvement.Create,Improvement.Edit,Measurement.View,Measurement.Create,Reports.View'
          WHERE [Code]='improvement-analyst' AND [IsSystemRole]=1;
        UPDATE [dbo].[RoleGroups] SET [Permissions]=N'Risk.View,Risk.Create,Risk.Edit,Risk.Approve,Incident.View,Problem.View,Service.View,Process.View,Asset.View,Reports.View,Reports.Export'
          WHERE [Code]='risk-manager' AND [IsSystemRole]=1;
    ");

    // Seed the four default role groups that match the tiered approval
    // recommendation from the earlier conversation. N-prefix the Unicode
    // literals so Arabic characters are preserved in nvarchar columns.
    //
    // DYN-001: the [Code] column MUST be set in this INSERT. The Code-tagging
    // UPDATE above runs BEFORE this INSERT, so on a fresh DB it matches zero
    // rows and these groups would otherwise be created with a NULL Code. The
    // test-user → RoleGroup assignment below joins on [Code] = 'process-owner'
    // etc., so a NULL Code silently breaks every test user's group membership.
    // (The UPDATE above is still useful: it back-fills Code on any rows that
    // were seeded by a previous boot before this fix.)
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM [dbo].[RoleGroups] WHERE [NameEn] = 'Quality Officer')
        BEGIN
            INSERT INTO [dbo].[RoleGroups] ([Id], [NameEn], [NameAr], [DescriptionEn], [DescriptionAr], [ScopeLevel], [Permissions], [Icon], [Color], [IsSystemRole], [Code])
            VALUES
                (NEWID(), N'Quality Officer', N'مسؤول الجودة',
                 N'Owns quality governance, reviews excellence submissions, approves initiatives at tier 2',
                 N'يملك حوكمة الجودة ويراجع طلبات التميز ويعتمد المبادرات في المستوى الثاني',
                 N'All', N'Improvement.View,Improvement.Approve,Improvement.Edit,Process.View,Service.View,Reports.View,Reports.Export', N'shield-check', N'#005B99', 1, N'quality-officer'),
                (NEWID(), N'Process Owner', N'مالك العملية',
                 N'Responsible for a specific business process and its improvement actions',
                 N'مسؤول عن عملية أعمال محددة وإجراءات تحسينها',
                 N'Process', N'Process.View,Process.Edit,Improvement.View,Improvement.Create,Improvement.Edit,Measurement.View', N'git-branch', N'#005B99', 1, N'process-owner'),
                (NEWID(), N'Improvement Analyst', N'محلل التحسين',
                 N'Submits and tracks improvement initiatives, monitors measurements',
                 N'يقدم ويتابع مبادرات التحسين ويراقب المقاييس',
                 N'OwningUnit', N'Improvement.View,Improvement.Create,Improvement.Edit,Measurement.View,Measurement.Create,Reports.View', N'trending-up', N'#005B99', 1, N'improvement-analyst'),
                (NEWID(), N'Risk Manager', N'مدير المخاطر',
                 N'Enterprise risk owner, reviews risk treatments and incidents',
                 N'مالك مخاطر المؤسسة، يراجع معالجات المخاطر والحوادث',
                 N'All', N'Risk.View,Risk.Create,Risk.Edit,Risk.Approve,Incident.View,Problem.View,Service.View,Process.View,Asset.View,Reports.View,Reports.Export', N'alert-triangle', N'#005B99', 1, N'risk-manager');
        END
    ");

    // Repair any already-seeded role groups that lost their Arabic text
    // due to the missing N-prefix on the first boot. This is idempotent.
    await context.Database.ExecuteSqlRawAsync(@"
        UPDATE [dbo].[RoleGroups] SET [NameAr] = N'مسؤول الجودة',
               [DescriptionAr] = N'يملك حوكمة الجودة ويراجع طلبات التميز ويعتمد المبادرات في المستوى الثاني'
          WHERE [NameEn] = 'Quality Officer' AND [NameAr] LIKE '%?%';
        UPDATE [dbo].[RoleGroups] SET [NameAr] = N'مالك العملية',
               [DescriptionAr] = N'مسؤول عن عملية أعمال محددة وإجراءات تحسينها'
          WHERE [NameEn] = 'Process Owner' AND [NameAr] LIKE '%?%';
        UPDATE [dbo].[RoleGroups] SET [NameAr] = N'محلل التحسين',
               [DescriptionAr] = N'يقدم ويتابع مبادرات التحسين ويراقب المقاييس'
          WHERE [NameEn] = 'Improvement Analyst' AND [NameAr] LIKE '%?%';
        UPDATE [dbo].[RoleGroups] SET [NameAr] = N'مدير المخاطر',
               [DescriptionAr] = N'مالك مخاطر المؤسسة، يراجع معالجات المخاطر والحوادث'
          WHERE [NameEn] = 'Risk Manager' AND [NameAr] LIKE '%?%';
    ");

    // Normalize all role-group accent colors to the ESEMS brand blue so the
    // card grid looks uniform instead of a rainbow. Admins can still
    // customize individual groups through the editor modal afterwards.
    await context.Database.ExecuteSqlRawAsync(@"
        UPDATE [dbo].[RoleGroups] SET [Color] = N'#005B99'
          WHERE [Color] IN (N'#10b981', N'#0ea5e9', N'#f59e0b', N'#ef4444');
    ");

    // Plan X: UserRoleGroups junction table — links users to role groups for
    // the matrix-driven authorization.
    // NOTE: no FK to dbo.user because that table uses snake_case (user_id)
    // and EF's cross-convention FK declarations get messy. The int UserId is
    // just treated as a plain key column — enforcement is via the unique
    // index + controller-side existence checks.
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserRoleGroups')
        BEGIN
            CREATE TABLE [dbo].[UserRoleGroups] (
                [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                [UserId] INT NOT NULL,
                [RoleGroupId] NVARCHAR(450) NOT NULL,
                [AssignedBy] INT NULL,
                [AssignedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [FK_UserRoleGroups_RoleGroups]
                    FOREIGN KEY ([RoleGroupId]) REFERENCES [dbo].[RoleGroups] ([Id]) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX [IX_UserRoleGroups_User_Group]
                ON [dbo].[UserRoleGroups] ([UserId], [RoleGroupId]);
            CREATE INDEX [IX_UserRoleGroups_UserId]
                ON [dbo].[UserRoleGroups] ([UserId]);
        END
    ");

    // Plan X: seed a system-wide 'Administrator' role group if missing. This
    // is what every user who used to have the legacy ADMIN role will be
    // mapped to on first login. We grant the global wildcard '*.*' rather
    // than an explicit per-module list: PermissionAuthorizationHandler
    // already treats '*.*' as a grant-all, and this keeps the seeded
    // Administrator group automatically in sync with any new module/action
    // added to AppPolicies (no separate backfill UPDATE needed each time).
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM [dbo].[RoleGroups] WHERE [Code] = 'administrator')
        BEGIN
            INSERT INTO [dbo].[RoleGroups]
                ([Id], [NameEn], [NameAr], [DescriptionEn], [DescriptionAr],
                 [ScopeLevel], [Permissions], [Icon], [Color], [IsActive],
                 [IsSystemRole], [Code], [CreatedAt], [UpdatedAt])
            VALUES
                (NEWID(), N'Administrator', N'مدير النظام',
                 N'Full system access — every module, every action',
                 N'وصول كامل للنظام — جميع الوحدات وجميع الإجراءات',
                 N'All',
                 N'*.*',
                 N'shield', N'#005B99', 1, 1, 'administrator', GETUTCDATE(), GETUTCDATE());
        END
    ");

    // Backfill: collapse any pre-existing Administrator group to the '*.*'
    // wildcard. Earlier seeds wrote an explicit per-module list that was
    // missing ChangeRequest.*, WorkflowTask.*, OrganizationUnit.*, etc., so
    // Plan-X-only admins (no legacy ADMIN row) couldn't manage those modules.
    // Idempotent — only touches rows whose Permissions string isn't already
    // exactly '*.*'.
    await context.Database.ExecuteSqlRawAsync(@"
        UPDATE [dbo].[RoleGroups]
        SET [Permissions] = N'*.*',
            [UpdatedAt] = GETUTCDATE()
        WHERE [Code] = 'administrator' AND [Permissions] <> N'*.*';
    ");

    // Plan X: seed a 'Viewer' read-only group if missing. Grants .View
    // across every business module plus Users/Settings/Ai/Workload View.
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM [dbo].[RoleGroups] WHERE [Code] = 'viewer')
        BEGIN
            INSERT INTO [dbo].[RoleGroups]
                ([Id], [NameEn], [NameAr], [DescriptionEn], [DescriptionAr],
                 [ScopeLevel], [Permissions], [Icon], [Color], [IsActive],
                 [IsSystemRole], [Code], [CreatedAt], [UpdatedAt])
            VALUES
                (NEWID(), N'Viewer', N'مشاهد',
                 N'Read-only access to every module',
                 N'وصول للقراءة فقط لجميع الوحدات',
                 N'All',
                 N'Improvement.View,Measurement.View,Process.View,Service.View,Risk.View,Asset.View,Incident.View,Problem.View,ChangeRequest.View,WorkflowTask.View,OrganizationUnit.View,Workflow.View,Reports.View,Users.View,Settings.View,Ai.View,Workload.View',
                 N'eye', N'#005B99', 1, 1, 'viewer', GETUTCDATE(), GETUTCDATE());
        END
    ");

    // Backfill Viewer to legacy parity — older seeds omitted
    // ChangeRequest.View, WorkflowTask.View, OrganizationUnit.View. Idempotent:
    // each append guard ('NOT LIKE %X%') only fires when the key is missing.
    await context.Database.ExecuteSqlRawAsync(@"
        UPDATE [dbo].[RoleGroups]
        SET [Permissions] = [Permissions] + N',ChangeRequest.View',
            [UpdatedAt] = GETUTCDATE()
        WHERE [Code] = 'viewer' AND [Permissions] NOT LIKE '%ChangeRequest.View%';

        UPDATE [dbo].[RoleGroups]
        SET [Permissions] = [Permissions] + N',WorkflowTask.View',
            [UpdatedAt] = GETUTCDATE()
        WHERE [Code] = 'viewer' AND [Permissions] NOT LIKE '%WorkflowTask.View%';

        UPDATE [dbo].[RoleGroups]
        SET [Permissions] = [Permissions] + N',OrganizationUnit.View',
            [UpdatedAt] = GETUTCDATE()
        WHERE [Code] = 'viewer' AND [Permissions] NOT LIKE '%OrganizationUnit.View%';

        UPDATE [dbo].[RoleGroups]
        SET [Permissions] = [Permissions] + N',Workload.View',
            [UpdatedAt] = GETUTCDATE()
        WHERE [Code] = 'viewer' AND [Permissions] NOT LIKE '%Workload.View%';
    ");

    // Plan X: seed an 'Editor' role group. Grants View/Create/Edit/Delete
    // (and Export where applicable) across every business module, but no
    // approval rights — those belong to the Approver group.
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM [dbo].[RoleGroups] WHERE [Code] = 'editor')
        BEGIN
            INSERT INTO [dbo].[RoleGroups]
                ([Id], [NameEn], [NameAr], [DescriptionEn], [DescriptionAr],
                 [ScopeLevel], [Permissions], [Icon], [Color], [IsActive],
                 [IsSystemRole], [Code], [CreatedAt], [UpdatedAt])
            VALUES
                (NEWID(), N'Editor', N'محرر',
                 N'Create/edit/delete across all modules (no approval)',
                 N'إنشاء وتعديل وحذف عبر جميع الوحدات (بدون اعتماد)',
                 N'All',
                 N'Improvement.View,Improvement.Create,Improvement.Edit,Improvement.Delete,Improvement.Export,Measurement.View,Measurement.Create,Measurement.Edit,Measurement.Delete,Measurement.Export,Process.View,Process.Create,Process.Edit,Process.Delete,Process.Export,Service.View,Service.Create,Service.Edit,Service.Delete,Service.Export,Risk.View,Risk.Create,Risk.Edit,Risk.Delete,Risk.Export,Asset.View,Asset.Create,Asset.Edit,Asset.Delete,Asset.Export,Incident.View,Incident.Create,Incident.Edit,Incident.Delete,Problem.View,Problem.Create,Problem.Edit,Problem.Delete,ChangeRequest.View,ChangeRequest.Create,ChangeRequest.Edit,ChangeRequest.Delete,WorkflowTask.View,WorkflowTask.Create,WorkflowTask.Edit,WorkflowTask.Delete,OrganizationUnit.View,OrganizationUnit.Create,OrganizationUnit.Edit,Workflow.View,Reports.View,Reports.Export,Ai.View',
                 N'edit-3', N'#005B99', 1, 1, 'editor', GETUTCDATE(), GETUTCDATE());
        END
    ");

    // Plan X: seed an 'Approver' role group. Grants View + Approve across
    // approval-bearing modules (Improvement, Risk, ChangeRequest, Workflow).
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM [dbo].[RoleGroups] WHERE [Code] = 'approver')
        BEGIN
            INSERT INTO [dbo].[RoleGroups]
                ([Id], [NameEn], [NameAr], [DescriptionEn], [DescriptionAr],
                 [ScopeLevel], [Permissions], [Icon], [Color], [IsActive],
                 [IsSystemRole], [Code], [CreatedAt], [UpdatedAt])
            VALUES
                (NEWID(), N'Approver', N'معتمد',
                 N'View + approve across approval-bearing modules',
                 N'عرض واعتماد عبر الوحدات التي تتطلب اعتماداً',
                 N'All',
                 N'Improvement.View,Improvement.Approve,Risk.View,Risk.Approve,Workflow.View,Workflow.Approve,ChangeRequest.View,ChangeRequest.Approve,WorkflowTask.View,Process.View,Service.View,Measurement.View,OrganizationUnit.View,Reports.View',
                 N'check-circle', N'#005B99', 1, 1, 'approver', GETUTCDATE(), GETUTCDATE());
        END
    ");

    // Seed a default one-level approval configuration for Improvement
    // initiatives, pointing at the admin user so the flow works out of the
    // box. Administrators can re-assign the approver later via the UI.
    // Guarded so we don't re-seed on every startup.
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT 1 FROM [dbo].[ApprovalConfigurations] WHERE [EntityType] = 'Improvement')
        BEGIN
            DECLARE @adminId INT = (SELECT TOP 1 [user_id] FROM [dbo].[user] WHERE [username] = 'admin' ORDER BY [user_id]);
            DECLARE @adminName NVARCHAR(200) = (SELECT TOP 1 ISNULL([employee_name], [full_name]) FROM [dbo].[user] WHERE [username] = 'admin');
            IF @adminId IS NOT NULL
            BEGIN
                INSERT INTO [dbo].[ApprovalConfigurations]
                    ([Id], [EntityType], [Level1Required], [Level1ApproverType], [Level1ApproverUserId], [Level1ApproverName],
                     [Level2Required], [IsActive], [CreatedAt], [UpdatedAt])
                VALUES
                    (NEWID(), 'Improvement', 1, 'SpecificUser', @adminId, @adminName, 0, 1, GETUTCDATE(), GETUTCDATE());
            END
        END
    ");

    // Document Management lookup tables (DocumentCategories, DocumentTypes)
    // Seeded from the Process Catalog reference sheet (full.xlsx → Sheet17).
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DocumentCategories')
        BEGIN
            CREATE TABLE [dbo].[DocumentCategories] (
                [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                [Code] NVARCHAR(50) NOT NULL,
                [NameEn] NVARCHAR(200) NOT NULL DEFAULT '',
                [NameAr] NVARCHAR(200) NOT NULL DEFAULT '',
                [DescriptionEn] NVARCHAR(MAX) NULL,
                [DescriptionAr] NVARCHAR(MAX) NULL,
                [DisplayOrder] INT NOT NULL DEFAULT 0,
                [IsActive] BIT NOT NULL DEFAULT 1,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [CreatedById] NVARCHAR(MAX) NULL,
                [UpdatedById] NVARCHAR(MAX) NULL,
                [Version] INT NOT NULL DEFAULT 1,
                [IsDeleted] BIT NOT NULL DEFAULT 0,
                [DeletedAt] DATETIME2 NULL
            );
            CREATE UNIQUE INDEX [IX_DocumentCategories_Code] ON [dbo].[DocumentCategories] ([Code]);
        END
    ");

    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DocumentTypes')
        BEGIN
            CREATE TABLE [dbo].[DocumentTypes] (
                [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                [Code] NVARCHAR(50) NOT NULL,
                [NameEn] NVARCHAR(200) NOT NULL DEFAULT '',
                [NameAr] NVARCHAR(200) NOT NULL DEFAULT '',
                [DescriptionEn] NVARCHAR(MAX) NULL,
                [DescriptionAr] NVARCHAR(MAX) NULL,
                [DisplayOrder] INT NOT NULL DEFAULT 0,
                [IsActive] BIT NOT NULL DEFAULT 1,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [CreatedById] NVARCHAR(MAX) NULL,
                [UpdatedById] NVARCHAR(MAX) NULL,
                [Version] INT NOT NULL DEFAULT 1,
                [IsDeleted] BIT NOT NULL DEFAULT 0,
                [DeletedAt] DATETIME2 NULL
            );
            CREATE UNIQUE INDEX [IX_DocumentTypes_Code] ON [dbo].[DocumentTypes] ([Code]);
        END
    ");

    // Add Document* FK columns to Processes if not already present.
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[Processes]') AND name = 'DocumentCategoryId')
        BEGIN
            ALTER TABLE [dbo].[Processes] ADD [DocumentCategoryId] NVARCHAR(450) NULL;
            CREATE INDEX [IX_Processes_DocumentCategoryId] ON [dbo].[Processes] ([DocumentCategoryId]);
        END
    ");
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[Processes]') AND name = 'DocumentTypeId')
        BEGIN
            ALTER TABLE [dbo].[Processes] ADD [DocumentTypeId] NVARCHAR(450) NULL;
            CREATE INDEX [IX_Processes_DocumentTypeId] ON [dbo].[Processes] ([DocumentTypeId]);
        END
    ");

    // Seed the 9 document categories + 24 document types from full.xlsx → Sheet17.
    // Gated by Bootstrap:RunSeeder for parity with the main SeedData call.
    if (app.Configuration.GetValue<bool>("Bootstrap:RunSeeder", true))
    {
        await ESEMS.Web.Data.DocumentLookupSeeder.SeedAsync(context);
        // Service classification lookup — backfills legacy free-text labels
        // and links existing services to the new FK on every startup.
        await ESEMS.Web.Data.ServiceCategorySeeder.SeedAsync(context);
    }

    // Process: drop legacy IsAutomated column — superseded by AutomationStatus enum.
    // The property is ignored in OnModelCreating so EF no longer maps it for Process.
    await context.Database.ExecuteSqlRawAsync(@"
        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[Processes]') AND name = 'IsAutomated')
        BEGIN
            ALTER TABLE [dbo].[Processes] DROP COLUMN [IsAutomated];
        END
    ");

    // ImprovementReviews — stage-gate review history (Green/Amber/Red health).
    // One initiative can have many reviews; cascade-deletes with the parent.
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ImprovementReviews')
        BEGIN
            CREATE TABLE [dbo].[ImprovementReviews] (
                [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                [ImprovementId] NVARCHAR(450) NOT NULL,
                [ReviewDate] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [HealthStatus] NVARCHAR(20) NOT NULL DEFAULT 'Green',
                [NotesEn] NVARCHAR(MAX) NULL,
                [NotesAr] NVARCHAR(MAX) NULL,
                [ProgressPercentageSnapshot] INT NULL,
                [NextReviewDate] DATETIME2 NULL,
                [ReviewedById] INT NULL,
                [ReviewedByName] NVARCHAR(200) NULL,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [FK_ImprovementReviews_ImprovementInitiatives]
                    FOREIGN KEY ([ImprovementId]) REFERENCES [dbo].[ImprovementInitiatives] ([Id]) ON DELETE CASCADE
            );
            CREATE INDEX [IX_ImprovementReviews_ImprovementId]
                ON [dbo].[ImprovementReviews] ([ImprovementId]);
            CREATE INDEX [IX_ImprovementReviews_NextReviewDate]
                ON [dbo].[ImprovementReviews] ([NextReviewDate]);
        END
    ");

    // ImprovementClosureReports — one formal closure record per initiative.
    // Lessons learned, sign-off, and closing comments. Cascade-deletes with
    // parent initiative; unique index on ImprovementId prevents duplicates.
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ImprovementClosureReports')
        BEGIN
            CREATE TABLE [dbo].[ImprovementClosureReports] (
                [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                [ImprovementId] NVARCHAR(450) NOT NULL,
                [LessonsLearnedEn] NVARCHAR(MAX) NULL,
                [LessonsLearnedAr] NVARCHAR(MAX) NULL,
                [ClosingComments] NVARCHAR(MAX) NULL,
                [SignedOffById] INT NULL,
                [SignedOffByName] NVARCHAR(200) NULL,
                [SignedOffAt] DATETIME2 NULL,
                [ClosedById] INT NULL,
                [ClosedByName] NVARCHAR(200) NULL,
                [ClosedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [FK_ImprovementClosureReports_ImprovementInitiatives]
                    FOREIGN KEY ([ImprovementId]) REFERENCES [dbo].[ImprovementInitiatives] ([Id]) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX [IX_ImprovementClosureReports_ImprovementId]
                ON [dbo].[ImprovementClosureReports] ([ImprovementId]);
        END
    ");

    // MeasurementReadings — time-series values collected during execution.
    // One row per period per measurement; unique index on (MeasurementId,
    // PeriodLabel) prevents double-entry. Used by Pass B to drive the
    // "reading due" reminder flow and the trend chart.
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MeasurementReadings')
        BEGIN
            CREATE TABLE [dbo].[MeasurementReadings] (
                [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                [MeasurementId] NVARCHAR(450) NOT NULL,
                [PeriodLabel] NVARCHAR(30) NOT NULL,
                [PeriodStart] DATETIME2 NOT NULL,
                [Value] DECIMAL(18, 4) NULL,
                [Notes] NVARCHAR(1000) NULL,
                [EnteredById] INT NULL,
                [EnteredAt] DATETIME2 NULL,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [FK_MeasurementReadings_ImprovementMeasurements]
                    FOREIGN KEY ([MeasurementId]) REFERENCES [dbo].[ImprovementMeasurements] ([Id]) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX [IX_MeasurementReadings_Measurement_Period]
                ON [dbo].[MeasurementReadings] ([MeasurementId], [PeriodLabel]);
            CREATE INDEX [IX_MeasurementReadings_PeriodStart]
                ON [dbo].[MeasurementReadings] ([PeriodStart]);
        END
    ");

    // ImprovementMeasurements: add Direction column (HigherBetter / LowerBetter)
    // so dashboards can colour improvements correctly. Existing rows stay NULL
    // which the entity treats as HigherBetter for backwards compatibility.
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[ImprovementMeasurements]') AND name = 'Direction')
        BEGIN
            ALTER TABLE [dbo].[ImprovementMeasurements] ADD [Direction] NVARCHAR(20) NULL;
        END
    ");

    // Backfill Direction for legacy rows:
    //   Cost (enum ord 1) and Time (enum ord 2) → LowerBetter
    //   Satisfaction / Productivity / Capacity / NumberOfVisits / NumberOfDocuments → HigherBetter
    //   Custom (7) stays NULL; the entity defaults to HigherBetter there
    // Guarded with WHERE Direction IS NULL so we don't overwrite explicit values.
    await context.Database.ExecuteSqlRawAsync(@"
        UPDATE [dbo].[ImprovementMeasurements]
        SET Direction = 'LowerBetter'
        WHERE Direction IS NULL AND MeasurementType IN (1, 2);

        UPDATE [dbo].[ImprovementMeasurements]
        SET Direction = 'HigherBetter'
        WHERE Direction IS NULL AND MeasurementType IN (0, 3, 4, 5, 6);
    ");

    // ImprovementRisks: junction table linking an ImprovementInitiative to an
    // EnterpriseRisk. Mirrors PManagement's InitiativeRiskLink pattern. The
    // composite PK on (ImprovementId, RiskId) guarantees one link per pair;
    // RelationshipType captures whether the initiative mitigates/creates the
    // risk so heat maps can separate "risks this creates" from "risks this
    // addresses".
    await context.Database.ExecuteSqlRawAsync(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ImprovementRisks')
        BEGIN
            CREATE TABLE [dbo].[ImprovementRisks] (
                [Id] NVARCHAR(450) NOT NULL,
                [ImprovementId] NVARCHAR(450) NOT NULL,
                [RiskId] NVARCHAR(450) NOT NULL,
                [RelationshipType] NVARCHAR(50) NOT NULL DEFAULT 'Mitigates',
                [ExpectedRiskReduction] INT NULL,
                [ImpactDescription] NVARCHAR(1000) NULL,
                [Notes] NVARCHAR(1000) NULL,
                [IsActive] BIT NOT NULL DEFAULT 1,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [CreatedById] NVARCHAR(450) NULL,
                [UpdatedById] NVARCHAR(450) NULL,
                CONSTRAINT [PK_ImprovementRisks]
                    PRIMARY KEY ([ImprovementId], [RiskId]),
                CONSTRAINT [FK_ImprovementRisks_ImprovementInitiatives]
                    FOREIGN KEY ([ImprovementId]) REFERENCES [dbo].[ImprovementInitiatives] ([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_ImprovementRisks_EnterpriseRisks]
                    FOREIGN KEY ([RiskId]) REFERENCES [dbo].[EnterpriseRisks] ([Id]) ON DELETE CASCADE
            );
            CREATE INDEX [IX_ImprovementRisks_RiskId]
                ON [dbo].[ImprovementRisks] ([RiskId]);
        END
    ");

    // ========================================================================
    // Hierarchical-code refactor (Categories=X / ProcessGroups=X.Y /
    // Processes=X.Y.Z). Adds SortKey (zero-padded for natural sort) on each
    // level, plus LegacyCode + ParentProcessId on Processes so the old
    // MP-/SP- relationship survives prefix removal. LegacyCode no longer
    // exists on Categories/ProcessGroups — only Processes keep it.
    // ========================================================================
    await context.Database.ExecuteSqlRawAsync(@"
        IF COL_LENGTH('dbo.Categories', 'SortKey') IS NULL
            ALTER TABLE [dbo].[Categories] ADD [SortKey] NVARCHAR(50) NULL;

        IF COL_LENGTH('dbo.ProcessGroups', 'SortKey') IS NULL
            ALTER TABLE [dbo].[ProcessGroups] ADD [SortKey] NVARCHAR(50) NULL;

        IF COL_LENGTH('dbo.Processes', 'LegacyCode') IS NULL
            ALTER TABLE [dbo].[Processes] ADD [LegacyCode] NVARCHAR(100) NULL;
        IF COL_LENGTH('dbo.Processes', 'SortKey') IS NULL
            ALTER TABLE [dbo].[Processes] ADD [SortKey] NVARCHAR(50) NULL;
        IF COL_LENGTH('dbo.Processes', 'ParentProcessId') IS NULL
        BEGIN
            ALTER TABLE [dbo].[Processes] ADD [ParentProcessId] NVARCHAR(450) NULL;
            CREATE INDEX [IX_Processes_ParentProcessId] ON [dbo].[Processes] ([ParentProcessId]);
        END
    ");

    // One-shot data migration. Runs every boot but is idempotent: any row
    // whose Code already matches the new format is left alone.
    await ESEMS.Web.Data.HierarchicalCodeMigration.RunAsync(context);

    // Cleanup: legacy seed runs accumulated duplicate JobPosition rows
    // (same Code, different Id) before this seeder was made per-row
    // idempotent. For every Code with >1 active row, keep the oldest one
    // (lowest CreatedAt, then lowest Id as tiebreaker) and soft-delete the
    // rest. Idempotent — runs every boot but does nothing on a clean DB.
    // Soft-delete (not DELETE) so existing RACI rows pointing at the
    // doomed Id resolve via the FK's OnDelete=SetNull behaviour rather
    // than failing the cleanup with a constraint violation.
    await context.Database.ExecuteSqlRawAsync(@"
        ;WITH ranked AS (
            SELECT [Id], [Code],
                   ROW_NUMBER() OVER (
                       PARTITION BY [Code]
                       ORDER BY [CreatedAt] ASC, [Id] ASC
                   ) AS rn
            FROM [dbo].[JobRoles]
            WHERE [IsDeleted] = 0 AND [Code] IS NOT NULL
        )
        UPDATE [dbo].[JobRoles]
        SET [IsDeleted]  = 1,
            [DeletedAt]  = GETUTCDATE(),
            [UpdatedAt]  = GETUTCDATE()
        WHERE [Id] IN (SELECT [Id] FROM ranked WHERE rn > 1);
    ");

    // Idempotent JobPosition catalog seed. SeedData.SeedAsync only runs on a
    // fully empty DB, so already-populated databases never received the 8
    // starter roles when migration AddJobPositions applied. We insert each
    // starter only if its Code isn't already present — per-row idempotency
    // means admins who hand-created a "TEST-ROLE" still get the canonical
    // 8 catalog entries on the next boot. Safe to re-run on any state.
    var starters = new[]
    {
        new ESEMS.Web.Models.Common.JobPosition { Code = "DIR",      NameEn = "Director",          NameAr = "مدير الإدارة",  Category = "Leadership",     IsLeadership = true,  DisplayOrder = 1 },
        new ESEMS.Web.Models.Common.JobPosition { Code = "DEP-DIR",  NameEn = "Deputy Director",   NameAr = "نائب المدير",   Category = "Leadership",     IsLeadership = true,  DisplayOrder = 2 },
        new ESEMS.Web.Models.Common.JobPosition { Code = "SEC-HEAD", NameEn = "Section Head",      NameAr = "رئيس قسم",      Category = "Leadership",     IsLeadership = true,  DisplayOrder = 3 },
        new ESEMS.Web.Models.Common.JobPosition { Code = "SR-SPEC",  NameEn = "Senior Specialist", NameAr = "أخصائي أول",    Category = "Specialist",     IsLeadership = false, DisplayOrder = 4 },
        new ESEMS.Web.Models.Common.JobPosition { Code = "SPEC",     NameEn = "Specialist",        NameAr = "أخصائي",        Category = "Specialist",     IsLeadership = false, DisplayOrder = 5 },
        new ESEMS.Web.Models.Common.JobPosition { Code = "COORD",    NameEn = "Coordinator",       NameAr = "منسق",          Category = "Administrative", IsLeadership = false, DisplayOrder = 6 },
        new ESEMS.Web.Models.Common.JobPosition { Code = "REVIEWER", NameEn = "Reviewer",          NameAr = "مراجع",         Category = "Specialist",     IsLeadership = false, DisplayOrder = 7 },
        new ESEMS.Web.Models.Common.JobPosition { Code = "OFFICER",  NameEn = "Officer",           NameAr = "موظف",          Category = "Administrative", IsLeadership = false, DisplayOrder = 8 }
    };
    // Per-row idempotency: filter to active (IsDeleted=false) rows when
    // counting "exists" — soft-deleted rows shouldn't block re-seeding.
    var existingCodes = await context.JobPositions
        .Where(j => !j.IsDeleted)
        .Select(j => j.Code)
        .ToListAsync();
    var existingCodeSet = new HashSet<string?>(existingCodes, StringComparer.OrdinalIgnoreCase);
    var missing = starters.Where(s => !existingCodeSet.Contains(s.Code)).ToList();
    if (missing.Count > 0)
    {
        context.JobPositions.AddRange(missing);
        await context.SaveChangesAsync();
    }
    });
}

// PathBase for IIS sub-application hosting (e.g. ejraa360.com/App).
// IIS sets ASPNETCORE_APPL_PATH automatically when the app is hosted as
// a sub-application; AppSettings:PathBase is the manual fallback. Without
// this every route 404s under sub-app hosting and cookie LoginPath /
// rate-limit StartsWith checks miss the /App prefix.
var pathBase = Environment.GetEnvironmentVariable("ASPNETCORE_APPL_PATH")
            ?? builder.Configuration["AppSettings:PathBase"];
if (!string.IsNullOrEmpty(pathBase) && pathBase != "/")
{
    app.UsePathBase(pathBase);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Friendly status-code pages. Re-execute routes 4xx/5xx responses through
// /Home/StatusCode/{code} so the user sees a themed "Not Found" / "Access
// Denied" page instead of a blank body. Runs in dev + prod so broken links
// behave consistently.
app.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}");

app.UseHttpsRedirection();

// Response compression MUST run before UseStaticFiles so static assets
// (JS, CSS, BPMN XML) get compressed too. Registered above with Brotli + Gzip.
app.UseResponseCompression();

// Security Headers (OWASP)
app.UseMiddleware<SecurityHeadersMiddleware>();

// Rate Limiting
app.UseRateLimiter();

app.UseStaticFiles();

// Request Localization
app.UseRequestLocalization();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Legacy URL redirect — /JobRoles* was the pre-May-2026 entity name; the
// renamed surface lives at /JobPositions* (controller, view folder, URLs
// all moved). 301 keeps old bookmarks/external links working and tells
// caches the new home is permanent. Anonymous-safe (no auth dependency).
// Matches both the bare /JobRoles and any sub-action like /JobRoles/Edit/{id}.
app.MapGet("/JobRoles/{**slug}", (HttpContext ctx, string? slug) =>
{
    var target = string.IsNullOrEmpty(slug) ? "/JobPositions" : $"/JobPositions/{slug}";
    var basePath = ctx.Request.PathBase.HasValue ? ctx.Request.PathBase.Value : string.Empty;
    var queryString = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value : string.Empty;
    ctx.Response.Redirect($"{basePath}{target}{queryString}", permanent: true);
    return Task.CompletedTask;
}).AllowAnonymous(); // The global FallbackPolicy requires authenticated users on
                     // every endpoint; without this the redirect would 401 first
                     // and an anonymous user with a stale bookmark would never see
                     // the 301. Redirecting is the whole point — let it through.

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

// Health check endpoints.
// RBAC-002 — the default ResponseWriter serializes every registered check
// (including `AddDbContextCheck<ApplicationDbContext>`) with status / duration
// / exception detail. Anonymous probes of `/health` were therefore disclosing
// "an SQL backend exists, version X, currently reachable" — and on failure
// leaking connection-string fragments via the exception message. Strip the
// response down to a flat "ok" / "unhealthy" so the load balancer still has
// a probe target without revealing infrastructure topology.
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "text/plain";
        await ctx.Response.WriteAsync(
            report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy
                ? "ok"
                : "unhealthy");
    }
}).AllowAnonymous();

// SignalR hub for real-time notifications
app.MapHub<NotificationHub>("/hubs/notifications");

// Initialize Database. Skipped under "Testing" — integration tests boot
// the app against in-memory EF and ensure the schema themselves, so the
// SqlServer migrate/seed path would throw if we tried to run it.
if (!app.Environment.IsEnvironment("Testing"))
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        // Swapped from Console.WriteLine → ILogger<Program> so startup
        // messages flow through the configured logging pipeline (console
        // provider in dev, file/Seq/App Insights in prod) instead of
        // bypassing it directly to stdout.
        logger.LogInformation("Starting database initialization (seed / post-migrate bootstrap)");
        var context = services.GetRequiredService<ApplicationDbContext>();

        if (DatabaseStartupRunner.EarlyBootstrapSkipped
            || await DatabaseStartupRunner.NeedsInitializationAsync(context))
        {
            logger.LogWarning(
                "Database is not fully initialized — running migration fallback (create catalog + apply migrations).");
            await DatabaseStartupRunner.ApplyMigrationsPipelineAsync(context, logger);
        }

        // Demo-data seeder DISABLED. The catalog is now populated exclusively
        // from the Excel imports (Org Structure, APQC Process Mapping, Services,
        // Asset Register). The old SeedData.SeedAsync planted a sample APQC
        // catalog — 24 demo processes plus their categories/groups and all the
        // dependent demo rows — which duplicated and got interleaved with the
        // imported data (mixed/inconsistent codes). We still seed the reference
        // lookups the importers depend on: DocumentLookupSeeder + ServiceCategorySeeder
        // (above) and the AssetCategory tree (here, required by the asset-register
        // import). Re-enable SeedData.SeedAsync only if you want demo data back.
        var runSeeder = app.Configuration.GetValue<bool>("Bootstrap:RunSeeder", true);
        if (runSeeder)
        {
            logger.LogInformation("Seeding reference lookups (asset categories); demo catalog seed is disabled");
            await ESEMS.Web.Data.AssetCategorySeeder.SeedAsync(context);
        }
        else
        {
            logger.LogInformation("Bootstrap:RunSeeder is false — skipping reference-lookup seeding");
        }

        // === ONE-TIME BPMN IMPORT ===
        // await ImportBpmnFromFiles(context); // Disabled: method is in AIController, not available here

        // ── Workload Analysis tables ──
        await context.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkloadConfigs')
            BEGIN
                CREATE TABLE [dbo].[WorkloadConfigs] (
                    [Id]                     NVARCHAR(450) NOT NULL PRIMARY KEY,
                    [NameEn]                 NVARCHAR(MAX) NOT NULL DEFAULT '',
                    [NameAr]                 NVARCHAR(MAX) NOT NULL DEFAULT '',
                    [DescriptionEn]          NVARCHAR(MAX) NULL,
                    [DescriptionAr]          NVARCHAR(MAX) NULL,
                    [WorkingHoursPerDay]     DECIMAL(18,2) NOT NULL DEFAULT 7.5,
                    [WorkingDaysPerWeek]     INT NOT NULL DEFAULT 5,
                    [PublicHolidaysPerYear]   INT NOT NULL DEFAULT 12,
                    [AnnualLeaveDays]        INT NOT NULL DEFAULT 22,
                    [AverageSickDays]        INT NOT NULL DEFAULT 7,
                    [TrainingDaysPerYear]    INT NOT NULL DEFAULT 7,
                    [AdminOverheadPercent]   DECIMAL(18,2) NOT NULL DEFAULT 15,
                    [TargetUtilizationRate]  DECIMAL(18,2) NOT NULL DEFAULT 0.80,
                    [SupervisoryRatio]       INT NOT NULL DEFAULT 8,
                    [OrganizationUnitId]     NVARCHAR(450) NULL,
                    [FiscalYearStart]        INT NOT NULL DEFAULT 1,
                    [CreatedAt]              DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [UpdatedAt]              DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [CreatedById]            NVARCHAR(MAX) NULL,
                    [UpdatedById]            NVARCHAR(MAX) NULL,
                    [Version]                INT NOT NULL DEFAULT 1,
                    [IsDeleted]              BIT NOT NULL DEFAULT 0,
                    [DeletedAt]              DATETIME2 NULL,
                    CONSTRAINT [FK_WorkloadConfigs_OrgUnit] FOREIGN KEY ([OrganizationUnitId])
                        REFERENCES [dbo].[OrganizationUnits]([Id]) ON DELETE SET NULL
                );
                CREATE INDEX [IX_WorkloadConfigs_OrganizationUnitId] ON [dbo].[WorkloadConfigs]([OrganizationUnitId]);
            END
        ");

        await context.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkloadScenarios')
            BEGIN
                CREATE TABLE [dbo].[WorkloadScenarios] (
                    [Id]                  NVARCHAR(450) NOT NULL PRIMARY KEY,
                    [NameEn]              NVARCHAR(MAX) NOT NULL DEFAULT '',
                    [NameAr]              NVARCHAR(MAX) NOT NULL DEFAULT '',
                    [DescriptionEn]       NVARCHAR(MAX) NULL,
                    [DescriptionAr]       NVARCHAR(MAX) NULL,
                    [Code]                NVARCHAR(450) NOT NULL DEFAULT '',
                    [Status]              INT NOT NULL DEFAULT 0,
                    [FiscalYear]          INT NOT NULL,
                    [OwningUnitId]        NVARCHAR(450) NULL,
                    [WorkloadConfigId]    NVARCHAR(450) NOT NULL,
                    [GrowthRatePercent]   DECIMAL(18,2) NULL,
                    [ProjectionYears]     INT NOT NULL DEFAULT 0,
                    [CurrentHeadcount]    INT NULL,
                    [Notes]               NVARCHAR(MAX) NULL,
                    [CreatedAt]           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [UpdatedAt]           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [CreatedById]         NVARCHAR(MAX) NULL,
                    [UpdatedById]         NVARCHAR(MAX) NULL,
                    [Version]             INT NOT NULL DEFAULT 1,
                    [IsDeleted]           BIT NOT NULL DEFAULT 0,
                    [DeletedAt]           DATETIME2 NULL,
                    CONSTRAINT [FK_WorkloadScenarios_Config] FOREIGN KEY ([WorkloadConfigId])
                        REFERENCES [dbo].[WorkloadConfigs]([Id]),
                    CONSTRAINT [FK_WorkloadScenarios_OrgUnit] FOREIGN KEY ([OwningUnitId])
                        REFERENCES [dbo].[OrganizationUnits]([Id]) ON DELETE SET NULL
                );
                CREATE UNIQUE INDEX [IX_WorkloadScenarios_Code] ON [dbo].[WorkloadScenarios]([Code]);
                CREATE INDEX [IX_WorkloadScenarios_FiscalYear_Unit] ON [dbo].[WorkloadScenarios]([FiscalYear], [OwningUnitId]);
            END
        ");

        await context.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkloadLineItems')
            BEGIN
                CREATE TABLE [dbo].[WorkloadLineItems] (
                    [Id]                       NVARCHAR(450) NOT NULL PRIMARY KEY,
                    [NameEn]                   NVARCHAR(MAX) NOT NULL DEFAULT '',
                    [NameAr]                   NVARCHAR(MAX) NOT NULL DEFAULT '',
                    [DescriptionEn]            NVARCHAR(MAX) NULL,
                    [DescriptionAr]            NVARCHAR(MAX) NULL,
                    [WorkloadScenarioId]       NVARCHAR(450) NOT NULL,
                    [ProcessId]                NVARCHAR(450) NULL,
                    [ServiceId]                NVARCHAR(450) NULL,
                    [AnnualVolume]             INT NOT NULL DEFAULT 0,
                    [AvgProcessingTimeMinutes] DECIMAL(18,2) NOT NULL DEFAULT 0,
                    [ComplexityEnabled]        BIT NOT NULL DEFAULT 0,
                    [SimpleVolumePercent]      DECIMAL(18,2) NULL DEFAULT 100,
                    [MediumVolumePercent]      DECIMAL(18,2) NULL,
                    [ComplexVolumePercent]      DECIMAL(18,2) NULL,
                    [SimpleMult]               DECIMAL(18,2) NULL DEFAULT 1.0,
                    [MediumMult]               DECIMAL(18,2) NULL DEFAULT 1.5,
                    [ComplexMult]              DECIMAL(18,2) NULL DEFAULT 2.5,
                    [SeasonalDistribution]     NVARCHAR(MAX) NULL,
                    [Notes]                    NVARCHAR(MAX) NULL,
                    [CreatedAt]                DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [UpdatedAt]                DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [CreatedById]              NVARCHAR(MAX) NULL,
                    [UpdatedById]              NVARCHAR(MAX) NULL,
                    [Version]                  INT NOT NULL DEFAULT 1,
                    [IsDeleted]                BIT NOT NULL DEFAULT 0,
                    [DeletedAt]                DATETIME2 NULL,
                    CONSTRAINT [FK_WorkloadLineItems_Scenario] FOREIGN KEY ([WorkloadScenarioId])
                        REFERENCES [dbo].[WorkloadScenarios]([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_WorkloadLineItems_Process] FOREIGN KEY ([ProcessId])
                        REFERENCES [dbo].[Processes]([Id]) ON DELETE SET NULL,
                    CONSTRAINT [FK_WorkloadLineItems_Service] FOREIGN KEY ([ServiceId])
                        REFERENCES [dbo].[Services]([Id]) ON DELETE SET NULL
                );
            END
        ");
        logger.LogInformation("Workload Analysis tables ensured");

        // ── Workload Analysis demo data — DISABLED by default (demo data removed
        //    during cleanup). Opt back in with Bootstrap:SeedWorkloadDemo=true.
        if (app.Configuration.GetValue<bool>("Bootstrap:SeedWorkloadDemo", false)
            && !await context.WorkloadScenarios.AnyAsync())
        {
            logger.LogInformation("Seeding Workload Analysis demo data");

            // Ensure default config exists
            var wlConfig = await context.WorkloadConfigs
                .FirstOrDefaultAsync(c => !c.IsDeleted && c.OrganizationUnitId == null);
            if (wlConfig == null)
            {
                wlConfig = new WorkloadConfig
                {
                    Id = Guid.NewGuid().ToString(),
                    NameEn = "UAE Government Default",
                    NameAr = "الإعدادات الافتراضية للحكومة الإماراتية",
                    DescriptionEn = "Default workload configuration with UAE government parameters (FAHR/DGHR standards)",
                    DescriptionAr = "إعدادات حجم العمل الافتراضية وفق معايير الحكومة الإماراتية (الهيئة الاتحادية للموارد البشرية)",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.WorkloadConfigs.Add(wlConfig);
                await context.SaveChangesAsync();
            }

            // Grab existing org units for linking
            var customerHappiness = await context.OrganizationUnits
                .FirstOrDefaultAsync(u => !u.IsDeleted && u.NameEn.Contains("Customer Happiness"));
            var housingCenter = await context.OrganizationUnits
                .FirstOrDefaultAsync(u => !u.IsDeleted && u.NameEn.Contains("Housing Center"));
            var supportServices = await context.OrganizationUnits
                .FirstOrDefaultAsync(u => !u.IsDeleted && u.NameEn.Contains("Support Services"));

            // Grab some processes and services for line items
            var processes = await context.Processes
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.Code)
                .Take(8)
                .ToListAsync();
            var svcList = await context.Services
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.Code)
                .Take(6)
                .ToListAsync();

            // ── Scenario 1: Customer Happiness Dept — FY2026, Approved ──
            var scenario1 = new WorkloadScenario
            {
                Id = Guid.NewGuid().ToString(),
                Code = "WS-001",
                NameEn = "FY2026 Customer Services Workload",
                NameAr = "حجم عمل خدمات العملاء 2026",
                DescriptionEn = "Annual FTE analysis for Customer Happiness Department service delivery operations",
                DescriptionAr = "تحليل سنوي للموارد البشرية لعمليات تقديم الخدمات في إدارة سعادة المتعاملين",
                FiscalYear = 2026,
                Status = ESEMS.Web.Models.Enums.WorkloadScenarioStatus.Approved,
                OwningUnitId = customerHappiness?.Id,
                WorkloadConfigId = wlConfig.Id,
                CurrentHeadcount = 32,
                GrowthRatePercent = 8,
                ProjectionYears = 1,
                Notes = "Based on 2025 actual transaction data + 8% projected growth for Smart Dubai initiatives",
                CreatedAt = DateTime.UtcNow.AddDays(-14),
                UpdatedAt = DateTime.UtcNow.AddDays(-3)
            };
            context.WorkloadScenarios.Add(scenario1);
            await context.SaveChangesAsync();

            // Line items for scenario 1 — mix of processes and services
            var lineItems1 = new List<WorkloadLineItem>();

            if (svcList.Count > 0)
            {
                lineItems1.Add(new WorkloadLineItem
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario1.Id,
                    ServiceId = svcList.ElementAtOrDefault(0)?.Id,
                    NameEn = svcList.ElementAtOrDefault(0)?.NameEn ?? "Housing Grant Application",
                    NameAr = svcList.ElementAtOrDefault(0)?.NameAr ?? "طلب منحة سكنية",
                    AnnualVolume = 4200,
                    AvgProcessingTimeMinutes = 45,
                    CreatedAt = DateTime.UtcNow.AddDays(-14),
                    UpdatedAt = DateTime.UtcNow.AddDays(-14)
                });
                lineItems1.Add(new WorkloadLineItem
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario1.Id,
                    ServiceId = svcList.ElementAtOrDefault(1)?.Id,
                    NameEn = svcList.ElementAtOrDefault(1)?.NameEn ?? "Maintenance Request",
                    NameAr = svcList.ElementAtOrDefault(1)?.NameAr ?? "طلب صيانة",
                    AnnualVolume = 8500,
                    AvgProcessingTimeMinutes = 25,
                    CreatedAt = DateTime.UtcNow.AddDays(-14),
                    UpdatedAt = DateTime.UtcNow.AddDays(-14)
                });
                lineItems1.Add(new WorkloadLineItem
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario1.Id,
                    ServiceId = svcList.ElementAtOrDefault(2)?.Id,
                    NameEn = svcList.ElementAtOrDefault(2)?.NameEn ?? "Housing Loan Inquiry",
                    NameAr = svcList.ElementAtOrDefault(2)?.NameAr ?? "استفسار قرض سكني",
                    AnnualVolume = 6300,
                    AvgProcessingTimeMinutes = 20,
                    CreatedAt = DateTime.UtcNow.AddDays(-14),
                    UpdatedAt = DateTime.UtcNow.AddDays(-14)
                });
            }
            if (processes.Count > 0)
            {
                lineItems1.Add(new WorkloadLineItem
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario1.Id,
                    ProcessId = processes.ElementAtOrDefault(0)?.Id,
                    NameEn = processes.ElementAtOrDefault(0)?.NameEn ?? "Application Eligibility Verification",
                    NameAr = processes.ElementAtOrDefault(0)?.NameAr ?? "التحقق من أهلية الطلب",
                    AnnualVolume = 4200,
                    AvgProcessingTimeMinutes = 35,
                    CreatedAt = DateTime.UtcNow.AddDays(-14),
                    UpdatedAt = DateTime.UtcNow.AddDays(-14)
                });
                lineItems1.Add(new WorkloadLineItem
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario1.Id,
                    ProcessId = processes.ElementAtOrDefault(1)?.Id,
                    NameEn = processes.ElementAtOrDefault(1)?.NameEn ?? "Customer Complaint Resolution",
                    NameAr = processes.ElementAtOrDefault(1)?.NameAr ?? "حل شكاوى المتعاملين",
                    AnnualVolume = 2100,
                    AvgProcessingTimeMinutes = 60,
                    CreatedAt = DateTime.UtcNow.AddDays(-14),
                    UpdatedAt = DateTime.UtcNow.AddDays(-14)
                });
            }
            lineItems1.Add(new WorkloadLineItem
            {
                Id = Guid.NewGuid().ToString(),
                WorkloadScenarioId = scenario1.Id,
                NameEn = "Phone & Walk-in Inquiries",
                NameAr = "الاستفسارات الهاتفية والمباشرة",
                AnnualVolume = 15000,
                AvgProcessingTimeMinutes = 10,
                CreatedAt = DateTime.UtcNow.AddDays(-14),
                UpdatedAt = DateTime.UtcNow.AddDays(-14)
            });
            lineItems1.Add(new WorkloadLineItem
            {
                Id = Guid.NewGuid().ToString(),
                WorkloadScenarioId = scenario1.Id,
                NameEn = "Document Verification & Archiving",
                NameAr = "التحقق من المستندات والأرشفة",
                AnnualVolume = 9000,
                AvgProcessingTimeMinutes = 15,
                ComplexityEnabled = true,
                SimpleVolumePercent = 60,
                MediumVolumePercent = 30,
                ComplexVolumePercent = 10,
                SimpleMult = 1.0m,
                MediumMult = 1.5m,
                ComplexMult = 3.0m,
                CreatedAt = DateTime.UtcNow.AddDays(-14),
                UpdatedAt = DateTime.UtcNow.AddDays(-14)
            });
            context.WorkloadLineItems.AddRange(lineItems1);

            // ── Scenario 2: Housing Center — FY2026, Draft ──
            var scenario2 = new WorkloadScenario
            {
                Id = Guid.NewGuid().ToString(),
                Code = "WS-002",
                NameEn = "FY2026 Housing Center Operations",
                NameAr = "عمليات مركز الإسكان 2026",
                DescriptionEn = "Staffing analysis for Dubai Integrated Housing Center front-office and back-office",
                DescriptionAr = "تحليل الموارد البشرية لمركز دبي المتكامل للإسكان - المكتب الأمامي والخلفي",
                FiscalYear = 2026,
                Status = ESEMS.Web.Models.Enums.WorkloadScenarioStatus.Draft,
                OwningUnitId = housingCenter?.Id,
                WorkloadConfigId = wlConfig.Id,
                CurrentHeadcount = 18,
                Notes = "Draft — pending volume data from Q1 2026",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            };
            context.WorkloadScenarios.Add(scenario2);
            await context.SaveChangesAsync();

            var lineItems2 = new List<WorkloadLineItem>
            {
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario2.Id,
                    ServiceId = svcList.ElementAtOrDefault(3)?.Id,
                    NameEn = svcList.ElementAtOrDefault(3)?.NameEn ?? "Counter Service Requests",
                    NameAr = svcList.ElementAtOrDefault(3)?.NameAr ?? "طلبات خدمة الكاونتر",
                    AnnualVolume = 12000,
                    AvgProcessingTimeMinutes = 20,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    UpdatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario2.Id,
                    NameEn = "Site Inspection Scheduling",
                    NameAr = "جدولة التفتيش الميداني",
                    AnnualVolume = 3600,
                    AvgProcessingTimeMinutes = 30,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    UpdatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario2.Id,
                    ProcessId = processes.ElementAtOrDefault(2)?.Id,
                    NameEn = processes.ElementAtOrDefault(2)?.NameEn ?? "Housing Unit Allocation",
                    NameAr = processes.ElementAtOrDefault(2)?.NameAr ?? "تخصيص الوحدات السكنية",
                    AnnualVolume = 2400,
                    AvgProcessingTimeMinutes = 90,
                    ComplexityEnabled = true,
                    SimpleVolumePercent = 40,
                    MediumVolumePercent = 40,
                    ComplexVolumePercent = 20,
                    SimpleMult = 1.0m,
                    MediumMult = 1.8m,
                    ComplexMult = 3.0m,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    UpdatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario2.Id,
                    NameEn = "Payment Processing",
                    NameAr = "معالجة المدفوعات",
                    AnnualVolume = 7200,
                    AvgProcessingTimeMinutes = 8,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    UpdatedAt = DateTime.UtcNow.AddDays(-5)
                }
            };
            context.WorkloadLineItems.AddRange(lineItems2);

            // ── Scenario 3: Support Services — FY2025, Approved (historical) ──
            var scenario3 = new WorkloadScenario
            {
                Id = Guid.NewGuid().ToString(),
                Code = "WS-003",
                NameEn = "FY2025 Support Services Retrospective",
                NameAr = "تحليل خدمات الدعم 2025 (استرجاعي)",
                DescriptionEn = "Retrospective workload analysis of Support Services Department for FY2025 benchmarking",
                DescriptionAr = "تحليل استرجاعي لحجم عمل إدارة خدمات الدعم لعام 2025 لأغراض المقارنة المعيارية",
                FiscalYear = 2025,
                Status = ESEMS.Web.Models.Enums.WorkloadScenarioStatus.Approved,
                OwningUnitId = supportServices?.Id,
                WorkloadConfigId = wlConfig.Id,
                CurrentHeadcount = 15,
                GrowthRatePercent = 5,
                ProjectionYears = 0,
                Notes = "Benchmark year — used as baseline for FY2026 planning",
                CreatedAt = DateTime.UtcNow.AddDays(-60),
                UpdatedAt = DateTime.UtcNow.AddDays(-45)
            };
            context.WorkloadScenarios.Add(scenario3);
            await context.SaveChangesAsync();

            var lineItems3 = new List<WorkloadLineItem>
            {
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario3.Id,
                    NameEn = "IT Support Tickets",
                    NameAr = "تذاكر الدعم الفني",
                    AnnualVolume = 5400,
                    AvgProcessingTimeMinutes = 35,
                    CreatedAt = DateTime.UtcNow.AddDays(-60),
                    UpdatedAt = DateTime.UtcNow.AddDays(-60)
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario3.Id,
                    NameEn = "Procurement Requests",
                    NameAr = "طلبات المشتريات",
                    AnnualVolume = 1800,
                    AvgProcessingTimeMinutes = 50,
                    CreatedAt = DateTime.UtcNow.AddDays(-60),
                    UpdatedAt = DateTime.UtcNow.AddDays(-60)
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario3.Id,
                    NameEn = "HR Requests (Leave, Certificates)",
                    NameAr = "طلبات الموارد البشرية (إجازات، شهادات)",
                    AnnualVolume = 3200,
                    AvgProcessingTimeMinutes = 15,
                    CreatedAt = DateTime.UtcNow.AddDays(-60),
                    UpdatedAt = DateTime.UtcNow.AddDays(-60)
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario3.Id,
                    NameEn = "Facility Maintenance Coordination",
                    NameAr = "تنسيق صيانة المرافق",
                    AnnualVolume = 2400,
                    AvgProcessingTimeMinutes = 25,
                    CreatedAt = DateTime.UtcNow.AddDays(-60),
                    UpdatedAt = DateTime.UtcNow.AddDays(-60)
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario3.Id,
                    NameEn = "Vendor Contract Administration",
                    NameAr = "إدارة عقود الموردين",
                    AnnualVolume = 600,
                    AvgProcessingTimeMinutes = 120,
                    CreatedAt = DateTime.UtcNow.AddDays(-60),
                    UpdatedAt = DateTime.UtcNow.AddDays(-60)
                }
            };
            context.WorkloadLineItems.AddRange(lineItems3);

            // ── Scenario 4: Customer Happiness — FY2026 In Review (what-if with automation) ──
            var scenario4 = new WorkloadScenario
            {
                Id = Guid.NewGuid().ToString(),
                Code = "WS-004",
                NameEn = "FY2026 Post-Automation Projection",
                NameAr = "إسقاط ما بعد الأتمتة 2026",
                DescriptionEn = "What-if scenario: projected staffing after automating 3 high-volume processes via Smart Dubai platform",
                DescriptionAr = "سيناريو افتراضي: الموارد البشرية المتوقعة بعد أتمتة 3 عمليات عالية الحجم عبر منصة دبي الذكية",
                FiscalYear = 2026,
                Status = ESEMS.Web.Models.Enums.WorkloadScenarioStatus.InReview,
                OwningUnitId = customerHappiness?.Id,
                WorkloadConfigId = wlConfig.Id,
                CurrentHeadcount = 32,
                GrowthRatePercent = 12,
                ProjectionYears = 2,
                Notes = "Compares against WS-001. Assumes 70% volume reduction on automated services.",
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            };
            context.WorkloadScenarios.Add(scenario4);
            await context.SaveChangesAsync();

            var lineItems4 = new List<WorkloadLineItem>
            {
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario4.Id,
                    NameEn = "Housing Grant Application (Automated)",
                    NameAr = "طلب منحة سكنية (مؤتمت)",
                    AnnualVolume = 1260, // 70% automated → only 30% manual
                    AvgProcessingTimeMinutes = 45,
                    Notes = "70% handled by Smart Dubai portal self-service",
                    CreatedAt = DateTime.UtcNow.AddDays(-7),
                    UpdatedAt = DateTime.UtcNow.AddDays(-7)
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario4.Id,
                    NameEn = "Maintenance Request (Automated)",
                    NameAr = "طلب صيانة (مؤتمت)",
                    AnnualVolume = 2550, // 70% automated
                    AvgProcessingTimeMinutes = 25,
                    Notes = "App-based maintenance requests with auto-routing",
                    CreatedAt = DateTime.UtcNow.AddDays(-7),
                    UpdatedAt = DateTime.UtcNow.AddDays(-7)
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario4.Id,
                    NameEn = "Housing Loan Inquiry (Chatbot)",
                    NameAr = "استفسار قرض سكني (روبوت محادثة)",
                    AnnualVolume = 1890, // 70% handled by chatbot
                    AvgProcessingTimeMinutes = 20,
                    Notes = "AI chatbot handles 70% of routine inquiries",
                    CreatedAt = DateTime.UtcNow.AddDays(-7),
                    UpdatedAt = DateTime.UtcNow.AddDays(-7)
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario4.Id,
                    NameEn = "Eligibility Verification (unchanged)",
                    NameAr = "التحقق من الأهلية (بدون تغيير)",
                    AnnualVolume = 4200,
                    AvgProcessingTimeMinutes = 35,
                    CreatedAt = DateTime.UtcNow.AddDays(-7),
                    UpdatedAt = DateTime.UtcNow.AddDays(-7)
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario4.Id,
                    NameEn = "Customer Complaint Resolution",
                    NameAr = "حل شكاوى المتعاملين",
                    AnnualVolume = 2100,
                    AvgProcessingTimeMinutes = 60,
                    CreatedAt = DateTime.UtcNow.AddDays(-7),
                    UpdatedAt = DateTime.UtcNow.AddDays(-7)
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    WorkloadScenarioId = scenario4.Id,
                    NameEn = "Phone & Walk-in Inquiries (reduced)",
                    NameAr = "الاستفسارات الهاتفية والمباشرة (مخفّض)",
                    AnnualVolume = 7500, // 50% reduction due to digital channels
                    AvgProcessingTimeMinutes = 10,
                    Notes = "50% shift to digital self-service channels",
                    CreatedAt = DateTime.UtcNow.AddDays(-7),
                    UpdatedAt = DateTime.UtcNow.AddDays(-7)
                }
            };
            context.WorkloadLineItems.AddRange(lineItems4);
            await context.SaveChangesAsync();

            logger.LogInformation("Workload Analysis seeded: 4 scenarios, {0} line items",
                lineItems1.Count + lineItems2.Count + lineItems3.Count + lineItems4.Count);
        }

        // Widen password column to support PBKDF2 hashes (was nvarchar(50), need 256)
        await context.Database.ExecuteSqlRawAsync(@"
            IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[user]') AND name = 'password' AND max_length < 512)
            BEGIN
                ALTER TABLE [dbo].[user] ALTER COLUMN [password] NVARCHAR(256) NULL;
            END
        ");

        // FUNC-006: add the soft-delete/active flag (idempotent). Existing rows
        // default to active. CustomUser.IsActive maps to this column; UsersController
        // deactivates instead of physically deleting, and login rejects inactive users.
        await context.Database.ExecuteSqlRawAsync(@"
            IF COL_LENGTH('[user]', 'is_active') IS NULL
            BEGIN
                ALTER TABLE [dbo].[user] ADD [is_active] BIT NOT NULL DEFAULT 1;
            END
        ");

        // Seed admin user into custom user table. Password from SeedAdmin:Password
        // (env var SeedAdmin__Password); defaults to Admin123 when unset — same as Development.
        // We never reset an existing admin password on boot.
        if (!await context.CustomUsers.AnyAsync(u => u.Username == "admin"))
        {
            var adminPassword = app.Configuration["SeedAdmin:Password"];

            // PROD-HARDENING (2026-06-03): never create the bootstrap admin with the
            // well-known weak default in Production. Refuse to start unless a strong
            // SeedAdmin__Password is supplied via env var / user-secrets — same posture
            // as a JWT-key placeholder guard. This only fires on FIRST boot (no admin
            // row yet); once the admin exists the whole block is skipped, so the env
            // var is mandatory only for the initial seed.
            if (app.Environment.IsProduction())
            {
                if (string.IsNullOrWhiteSpace(adminPassword) ||
                    string.Equals(adminPassword, "Admin123", StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Set a strong SeedAdmin__Password (environment variable) before the first " +
                        "Production boot — a blank or default 'Admin123' admin password is not allowed " +
                        "in Production. Change it again from the UI after first login.");
            }
            else if (string.IsNullOrWhiteSpace(adminPassword))
            {
                adminPassword = "Admin123"; // dev / staging convenience only
            }

            logger.LogInformation("Seeding admin user");
                context.CustomUsers.Add(new CustomUser
                {
                    Username = "admin",
                    Password = PasswordHelper.Hash(adminPassword),
                    EmailAddress = "admin@mbrhe.gov.ae",
                    EmployeeName = "System Administrator",
                    EmployeeNameAr = "مدير النظام",
                    FullName = "System Administrator",
                    EmployeeNumber = "ADMIN-001",
                    JobName = "System Administrator",
                    JobNameAr = "مدير النظام",
                });
                await context.SaveChangesAsync();
                logger.LogInformation("Admin user seeded");
        }

        // Self-heal: make sure the seeded `admin` user always carries the
        // Administrator RoleGroup, even if the user row pre-dated the Plan X
        // rollout. Idempotent — only inserts when the membership is missing,
        // so this is safe to run on every boot.
        await context.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (
                SELECT 1 FROM [dbo].[UserRoleGroups] urg
                INNER JOIN [dbo].[user] u ON u.[user_id] = urg.[UserId]
                INNER JOIN [dbo].[RoleGroups] rg ON rg.[Id] = urg.[RoleGroupId]
                WHERE u.[username] = 'admin' AND rg.[Code] = 'administrator'
            )
            BEGIN
                INSERT INTO [dbo].[UserRoleGroups] ([Id], [UserId], [RoleGroupId], [AssignedBy], [AssignedAt])
                SELECT NEWID(), u.[user_id], rg.[Id], u.[user_id], GETUTCDATE()
                FROM [dbo].[user] u, [dbo].[RoleGroups] rg
                WHERE u.[username] = 'admin' AND rg.[Code] = 'administrator';
            END
        ");

        // ── Seed test users for role-based testing ──
        // C1: test fixtures (weak passwords) seed ONLY in Development — never in
        // Production/Staging. Real prod role-group users are created via the Users
        // admin UI; the system role-group permission self-heal above still runs in prod.
        if (app.Environment.IsDevelopment())
        {
        var testUsers = new[]
        {
            ("viewer",    "Viewer123",  "viewer@mbrhe.gov.ae",  "Test Viewer",         "مشاهد اختبار",        "viewer",              "Customer Happiness"),
            ("editor",    "Editor123",  "editor@mbrhe.gov.ae",  "Test Editor",         "محرر اختبار",         "editor",              "Customer Happiness"),
            ("approver",  "Approv123",  "approver@mbrhe.gov.ae","Test Approver",       "معتمد اختبار",        "approver",            "Support Services"),
            ("quality",   "Qualit123",  "quality@mbrhe.gov.ae", "Quality Officer",     "مسؤول الجودة",        "quality-officer",     "Customer Happiness"),
            ("procowner", "ProcOw123",  "procown@mbrhe.gov.ae", "Process Owner",       "مالك العملية",         "process-owner",       "Housing"),
            ("analyst",   "Analys123",  "analyst@mbrhe.gov.ae", "Improvement Analyst", "محلل تحسين",          "improvement-analyst", "Support Services"),
            ("riskman",   "RiskMa123",  "risk@mbrhe.gov.ae",    "Risk Manager",        "مدير المخاطر",        "risk-manager",        "Support Services"),
        };

        foreach (var (uname, pass, email, nameEn, nameAr, roleGroupCode, unitHint) in testUsers)
        {
            // Resolve (or create) the test user. The RoleGroup assignment below
            // runs on EVERY boot so a previously-broken assignment self-heals.
            var testUser = await context.CustomUsers.FirstOrDefaultAsync(u => u.Username == uname);
            if (testUser == null)
            {
                // Find org unit
                var unit = await context.OrganizationUnits
                    .FirstOrDefaultAsync(u => u.NameEn != null && u.NameEn.Contains(unitHint));

                testUser = new CustomUser
                {
                    Username = uname,
                    Password = PasswordHelper.Hash(pass),
                    EmailAddress = email,
                    EmployeeName = nameEn,
                    EmployeeNameAr = nameAr,
                    FullName = nameEn,
                    EmployeeNumber = $"TEST-{uname.ToUpper()}",
                    JobName = nameEn,
                    JobNameAr = nameAr,
                    UnitId = unit?.Id,
                };
                context.CustomUsers.Add(testUser);
                await context.SaveChangesAsync();

                logger.LogInformation("Seeded test user: {Username} (RoleGroup={RoleGroup})", uname, roleGroupCode);
            }

            // Assign RoleGroup — runs REGARDLESS of whether the user already
            // existed, so a previously-broken assignment is repaired on the
            // next boot. Idempotent (IF NOT EXISTS) and a no-op if the
            // matching RoleGroup Code isn't found.
            await context.Database.ExecuteSqlRawAsync($@"
                IF NOT EXISTS (SELECT 1 FROM [dbo].[UserRoleGroups] WHERE [UserId] = {{0}}
                               AND [RoleGroupId] = (SELECT [Id] FROM [dbo].[RoleGroups] WHERE [Code] = {{1}}))
                BEGIN
                    INSERT INTO [dbo].[UserRoleGroups] ([Id], [UserId], [RoleGroupId], [AssignedBy], [AssignedAt])
                    SELECT NEWID(), {{0}}, [Id], 1, GETUTCDATE()
                    FROM [dbo].[RoleGroups] WHERE [Code] = {{1}};
                END", testUser.UserId, roleGroupCode);
        }
        } // C1: end Development-only test-user gate
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database seed / post-migrate bootstrap.");
        if (!app.Configuration.GetValue("Database:ContinueOnMigrationFailure", false))
            throw;
    }
}

app.Run();

// Expose the top-level Program type so WebApplicationFactory<Program> in the
// test project can boot the app without needing a separate Startup class.
public partial class Program { }
