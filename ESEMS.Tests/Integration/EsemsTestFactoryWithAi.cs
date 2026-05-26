using ESEMS.Web.Data;
using ESEMS.Web.Services.AI;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ESEMS.Tests.Integration;

/// <summary>
/// Test factory variant that also swaps <see cref="IAIService"/> for a
/// stub returning canned text. Uses real HTTP test code paths through
/// <c>AIController</c> without actually calling Azure OpenAI / AWS Bedrock.
/// </summary>
public sealed class EsemsTestFactoryWithAi : WebApplicationFactory<Program>
{
    private readonly string _dbName = "esems-ai-test-" + Guid.NewGuid();

    public StubAiService Ai { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(ApplicationDbContext) ||
                d.ServiceType == typeof(IAIService) ||
                (d.ServiceType.FullName ?? "").StartsWith("Microsoft.EntityFrameworkCore") ||
                (d.ImplementationType?.FullName ?? "").StartsWith("Microsoft.EntityFrameworkCore"))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<ApplicationDbContext>(opts => opts.UseInMemoryDatabase(_dbName));
            services.AddSingleton<IAIService>(Ai);

            services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // TestServer doesn't expose IConnectionItemsFeature; without this
            // shim, Program.cs's Negotiate auth handler 500s on every request.
            // Same fix lives in EsemsTestFactory — see that class for the
            // detailed rationale. Reused here verbatim (kept internal-sealed
            // to each factory rather than extracting to a shared utility so
            // each factory's wiring stays self-contained).
            services.AddSingleton<IStartupFilter, EsemsTestFactory.ConnectionItemsStartupFilter>();
        });
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

public sealed class StubAiService : IAIService
{
    public List<string> Calls { get; } = new();

    private string Record(string name, params object?[] args)
    {
        Calls.Add($"{name}({string.Join(", ", args)})");
        return $"[stub:{name}]";
    }

    public Task<string> GenerateProcessImprovementSuggestionsAsync(string processName, string processDescription, string? currentIssues = null)
        => Task.FromResult(Record(nameof(GenerateProcessImprovementSuggestionsAsync), processName));

    public Task<string> AnalyzeRiskAndSuggestMitigationAsync(string riskDescription, string riskCategory, int likelihood, int impact)
        => Task.FromResult(Record(nameof(AnalyzeRiskAndSuggestMitigationAsync), riskCategory, likelihood, impact));

    public Task<string> GenerateRACISuggestionsAsync(string activityName, string activityDescription, string organizationContext)
        => Task.FromResult(Record(nameof(GenerateRACISuggestionsAsync), activityName));

    public Task<string> SummarizeAuditLogsAsync(List<string> auditLogEntries, string timeframe)
        => Task.FromResult(Record(nameof(SummarizeAuditLogsAsync), auditLogEntries.Count, timeframe));

    public Task<string> GenerateStrategicObjectiveRecommendationsAsync(string organizationGoals, string currentObjectives)
        => Task.FromResult(Record(nameof(GenerateStrategicObjectiveRecommendationsAsync)));

    public Task<string> AnalyzeServicePerformanceAsync(string serviceName, decimal? satisfactionScore, int? transactionCount, string? issues)
        => Task.FromResult(Record(nameof(AnalyzeServicePerformanceAsync), serviceName, satisfactionScore, transactionCount));

    public Task<InitiativeScoringSuggestion> SuggestInitiativeScoringAsync(string titleEn, string titleAr, string? descriptionEn, string? descriptionAr, string? processName, string? scope)
    {
        Record(nameof(SuggestInitiativeScoringAsync), titleEn);
        return Task.FromResult(new InitiativeScoringSuggestion
        {
            Impact = 5,
            Effort = 5,
            Reasoning = "[stub] no AI in tests"
        });
    }

    public Task<(string English, string Arabic)> GenerateBilingualContentAsync(string prompt, string context)
    {
        Record(nameof(GenerateBilingualContentAsync), prompt);
        return Task.FromResult(("[stub-en]", "[stub-ar]"));
    }

    public Task<string> ChatAsync(string userMessage, List<(string role, string content)>? conversationHistory = null)
        => Task.FromResult(Record(nameof(ChatAsync), userMessage, conversationHistory?.Count ?? 0));

    public Task<string> GenerateBPMNDiagramAsync(string processName, string processDescription, List<string>? steps = null, bool? hintArabic = null)
        => Task.FromResult("<?xml version=\"1.0\"?><bpmn:definitions xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"/>");

    public Task<string> RefineBPMNDiagramAsync(string currentBpmnXml, string updateInstructions)
        => Task.FromResult(currentBpmnXml);

    public Task<string> ConvertVisioToBPMNAsync(string visioXmlContent, string? additionalContext = null, IReadOnlyList<string>? previousErrors = null)
        => Task.FromResult("<?xml version=\"1.0\"?><bpmn:definitions xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"/>");
}
