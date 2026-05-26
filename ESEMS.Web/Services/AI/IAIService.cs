namespace ESEMS.Web.Services.AI;

/// <summary>
/// Structured result for <see cref="IAIService.SuggestInitiativeScoringAsync"/>.
/// </summary>
public class InitiativeScoringSuggestion
{
    /// <summary>Suggested Impact score, 1-10 (10 = highest strategic impact).</summary>
    public int Impact { get; set; } = 5;
    /// <summary>Suggested Effort score, 1-10 (10 = highest delivery effort).</summary>
    public int Effort { get; set; } = 5;
    /// <summary>Short rationale (bilingual or single-locale) explaining the suggested scores.</summary>
    public string Reasoning { get; set; } = string.Empty;
    /// <summary>
    /// Derived label for the impact/effort quadrant so the UI doesn't need
    /// to repeat the threshold logic: 'QuickWin' | 'BigBet' | 'Fill-In' | 'ThanklessTask'.
    /// </summary>
    public string Quadrant { get; set; } = "Fill-In";
}

/// <summary>
/// Interface for AI-powered features using Azure OpenAI
/// </summary>
public interface IAIService
{
    /// <summary>
    /// Generate process improvement suggestions based on process data
    /// </summary>
    Task<string> GenerateProcessImprovementSuggestionsAsync(string processName, string processDescription, string? currentIssues = null);

    /// <summary>
    /// Analyze risk and suggest mitigation strategies
    /// </summary>
    Task<string> AnalyzeRiskAndSuggestMitigationAsync(string riskDescription, string riskCategory, int likelihood, int impact);

    /// <summary>
    /// Generate RACI matrix suggestions for an activity
    /// </summary>
    Task<string> GenerateRACISuggestionsAsync(string activityName, string activityDescription, string organizationContext);

    /// <summary>
    /// Summarize audit logs for reporting
    /// </summary>
    Task<string> SummarizeAuditLogsAsync(List<string> auditLogEntries, string timeframe);

    /// <summary>
    /// Generate strategic objective recommendations
    /// </summary>
    Task<string> GenerateStrategicObjectiveRecommendationsAsync(string organizationGoals, string currentObjectives);

    /// <summary>
    /// Analyze service performance and suggest improvements
    /// </summary>
    Task<string> AnalyzeServicePerformanceAsync(string serviceName, decimal? satisfactionScore, int? transactionCount, string? issues);

    /// <summary>
    /// Generate bilingual content (Arabic/English) for descriptions
    /// </summary>
    Task<(string English, string Arabic)> GenerateBilingualContentAsync(string prompt, string context);

    /// <summary>
    /// Chat with AI assistant for general queries
    /// </summary>
    Task<string> ChatAsync(string userMessage, List<(string role, string content)>? conversationHistory = null);

    /// <summary>
    /// Suggest Impact (1-10) and Effort (1-10) scores for an improvement
    /// initiative based on its title, description, and linked context.
    /// Returns scores plus a short reasoning string so the wizard can
    /// surface the rationale next to the sliders it pre-fills.
    /// </summary>
    Task<InitiativeScoringSuggestion> SuggestInitiativeScoringAsync(string titleEn, string titleAr, string? descriptionEn, string? descriptionAr, string? processName, string? scope);

    /// <summary>
	/// Generate BPMN 2.0 process diagram as BPMN 2.0 XML (importable by bpmn-js)
    /// </summary>
    Task<string> GenerateBPMNDiagramAsync(string processName, string processDescription, List<string>? steps = null, bool? hintArabic = null);

    /// <summary>
	/// Refine/update an existing BPMN 2.0 XML diagram based on user instructions
    /// </summary>
	Task<string> RefineBPMNDiagramAsync(string currentBpmnXml, string updateInstructions);

    /// <summary>
	/// Convert Visio XML content to BPMN 2.0 XML (importable by bpmn-js)
    /// </summary>
    Task<string> ConvertVisioToBPMNAsync(string visioXmlContent, string? additionalContext = null, IReadOnlyList<string>? previousErrors = null);
}

