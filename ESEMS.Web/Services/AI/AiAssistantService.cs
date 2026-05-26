using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;

namespace ESEMS.Web.Services.AI;

/// <summary>
/// RAG (Retrieval-Augmented Generation) AI assistant.
/// Searches the vector store for context, enriches with DB stats, and calls Azure OpenAI.
/// </summary>
public class AiAssistantService
{
    private readonly VectorStoreService _vectorStore;
    private readonly IAIService _aiService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiAssistantService> _logger;

    public AiAssistantService(
        VectorStoreService vectorStore,
        IAIService aiService,
        IServiceProvider serviceProvider,
        ILogger<AiAssistantService> logger)
    {
        _vectorStore = vectorStore;
        _aiService = aiService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<AiAssistantResponse> AskAsync(string question, List<(string role, string content)>? conversationHistory = null, string? culture = null)
    {
        // Detect language: explicit culture wins, otherwise fall back to current UI culture
        var isArabic = (culture ?? System.Globalization.CultureInfo.CurrentUICulture.Name)
            .StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        try
        {
            // Ensure index is populated
            if (_vectorStore.DocumentCount == 0)
                await _vectorStore.IndexAllDataAsync();

            // 1. Search vector store for relevant context
            var relevantDocs = _vectorStore.Search(question, topK: 15);

            // 2. Enrich with database statistics
            var dbStats = await GetDatabaseStatsAsync();

            // 3. Build system prompt with context
            var contextSection = "";
            if (relevantDocs.Any())
            {
                contextSection = "## Relevant Information from the System\n\n" +
                    string.Join("\n", relevantDocs.Select(d =>
                        $"- **[{d.EntityType}]** {d.Title}: {d.Content[..Math.Min(200, d.Content.Length)]}"));
            }

            var systemPrompt = $@"You are the ESEMS AI Assistant for Mohammed Bin Rashid Housing Establishment (MBRHE).
You help users understand processes, services, risks, incidents, improvements, and overall organizational performance.

## Platform Overview
ESEMS manages:
- Enterprise Processes (APQC framework, 5 levels)
- Service Catalog with SLA monitoring
- Incident & Problem Management (ISO 20000-1)
- Enterprise Risk Management (ISO 31000)
- Continuous Improvement Initiatives
- Asset Management (ISO 55001)
- Customer Feedback
- Change Requests

## Current System Statistics
{dbStats}

{contextSection}

## Instructions
- {(isArabic
    ? "**CRITICAL**: The user is using the Arabic version of the application. You MUST respond ENTIRELY in Arabic (العربية) — including all headings, lists, explanations, and any examples. Do not switch to English even if the user's question contains English words or product names."
    : "Respond in English. If the user explicitly writes in Arabic, you may answer in Arabic.")}
- Be concise but thorough
- Reference specific data from the system when available
- Suggest 2-3 follow-up questions the user might want to ask
- If you don't have enough information, say so honestly
- Format responses with markdown for readability";

            // 4. Build conversation with context
            var messages = new List<(string role, string content)>
            {
                ("system", systemPrompt)
            };

            // Add last 4 conversation messages for context
            if (conversationHistory?.Any() == true)
            {
                messages.AddRange(conversationHistory.TakeLast(4));
            }

            // 5. Call AI
            var answer = await _aiService.ChatAsync(question, messages);

            // 6. Generate suggestions (localized)
            var suggestions = GenerateSuggestions(question, relevantDocs, isArabic);

            return new AiAssistantResponse
            {
                Answer = answer,
                Suggestions = suggestions,
                SourceCount = relevantDocs.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Assistant error for question: {Question}", question);
            return new AiAssistantResponse
            {
                Answer = isArabic
                    ? "أعتذر، حدث خطأ أثناء معالجة سؤالك. الرجاء المحاولة مرة أخرى."
                    : "I apologize, but I encountered an error processing your question. Please try again.",
                Suggestions = isArabic
                    ? new List<string> { "ما هي العمليات التي تديرها مؤسسة محمد بن راشد للإسكان؟", "اعرض لي نظرة عامة على المخاطر" }
                    : new List<string> { "What processes does MBRHE manage?", "Show me the risk overview" }
            };
        }
    }

    private async Task<string> GetDatabaseStatsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var processCount = await context.Processes.CountAsync(p => !p.IsDeleted);
        var serviceCount = await context.Services.CountAsync(s => !s.IsDeleted);
        var incidentCount = await context.Incidents.CountAsync(i => !i.IsDeleted);
        var problemCount = await context.Problems.CountAsync(p => !p.IsDeleted);
        var riskCount = await context.EnterpriseRisks.CountAsync(r => !r.IsDeleted);
        var improvementCount = await context.ImprovementInitiatives.CountAsync(i => !i.IsDeleted);
        var openIncidents = await context.Incidents.CountAsync(i => !i.IsDeleted &&
            i.Status != Models.Enums.IncidentStatus.Resolved && i.Status != Models.Enums.IncidentStatus.Closed);

        return $@"- Total Processes: {processCount}
- Total Services: {serviceCount}
- Total Incidents: {incidentCount} ({openIncidents} open)
- Total Problems: {problemCount}
- Enterprise Risks: {riskCount}
- Improvement Initiatives: {improvementCount}";
    }

    private static List<string> GenerateSuggestions(string question, List<IndexedDocument> docs, bool isArabic)
    {
        var suggestions = new List<string>();
        var entityTypes = docs.Select(d => d.EntityType).Distinct().ToList();

        if (entityTypes.Contains("Process"))
            suggestions.Add(isArabic ? "ما هي المخاطر الرئيسية المرتبطة بهذه العمليات؟" : "What are the key risks associated with these processes?");
        if (entityTypes.Contains("Incident"))
            suggestions.Add(isArabic ? "ما هي مبادرات التحسين التي تعالج هذه الحوادث؟" : "What improvement initiatives address these incidents?");
        if (entityTypes.Contains("Risk"))
            suggestions.Add(isArabic ? "ما هي خطط التخفيف الموجودة لهذه المخاطر؟" : "What mitigation plans are in place for these risks?");
        if (!entityTypes.Any())
        {
            if (isArabic)
            {
                suggestions.Add("ما هي العمليات التي تديرها المؤسسة؟");
                suggestions.Add("اعرض لي نظرة عامة على المخاطر الحالية");
                suggestions.Add("ما هي مبادرات التحسين الجارية؟");
            }
            else
            {
                suggestions.Add("What processes does MBRHE manage?");
                suggestions.Add("Show me the current risk overview");
                suggestions.Add("What improvement initiatives are in progress?");
            }
        }

        return suggestions.Take(3).ToList();
    }
}

public class AiAssistantResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<string> Suggestions { get; set; } = new();
    public int SourceCount { get; set; }
}
