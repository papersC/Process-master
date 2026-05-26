namespace ESEMS.Web.Models.ViewModels;

/// <summary>
/// Drop-in model for the shared <c>_AiAnalysisPanel</c> partial. Lets any
/// Details view embed an inline AI evaluation card without rolling its own
/// fetch + render JS.
/// </summary>
public class AiAnalysisPanelVM
{
    /// <summary>The AIController endpoint to POST to (e.g. "/AI/AnalyzeEnterpriseRisk?riskId=abc").</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Card heading shown to the user (e.g. "AI Risk Analysis").</summary>
    public string Title { get; set; } = "AI Analysis";

    /// <summary>One-line description shown under the title.</summary>
    public string Subtitle { get; set; } = "";

    /// <summary>Unique DOM id suffix — required when more than one panel renders on the same page.</summary>
    public string PanelId { get; set; } = "default";

    /// <summary>Optional JSON body posted to the endpoint. When null the endpoint reads only its query string.</summary>
    public string? BodyJson { get; set; }
}
