namespace ESEMS.Web.Services.Analysis;

public class AnalysisContext
{
    public string ContextMarkdown { get; set; } = string.Empty;
    public int ActivityCount { get; set; }
    public int RiskCount { get; set; }
    public int IncidentCount { get; set; }
    public int ProblemCount { get; set; }
    public int ImprovementCount { get; set; }
    public bool HasBpmn { get; set; }
}

public interface IProcessAnalysisService
{
    Task<AnalysisContext> BuildProcessGroupAnalysisContextAsync(string processGroupId);
    Task<AnalysisContext> BuildProcessAnalysisContextAsync(string processId);
    string BuildOptimizedBpmnPrompt(bool isArabic = false);
    string CleanOptimizedPromptResponse(string response);
}
