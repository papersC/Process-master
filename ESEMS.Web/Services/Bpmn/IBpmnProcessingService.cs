namespace ESEMS.Web.Services.Bpmn;

public interface IBpmnProcessingService
{
    string CleanBpmnXml(string raw);
    bool LooksLikeBpmnXml(string text);
}
