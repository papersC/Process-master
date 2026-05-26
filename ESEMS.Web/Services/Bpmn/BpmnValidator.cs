using System.Xml.Linq;

namespace ESEMS.Web.Services.Bpmn;

/// <summary>
/// Structural validator for BPMN 2.0 XML produced by the Visio→BPMN
/// conversion pipeline (Bedrock). Catches the common failure modes:
/// malformed XML, missing process, empty diagrams, and DI references
/// that point to nonexistent process elements (which render as blank
/// shapes in bpmn-js).
/// </summary>
public sealed class BpmnValidator : IBpmnValidator
{
    private static readonly XNamespace BpmnNs  = "http://www.omg.org/spec/BPMN/20100524/MODEL";
    private static readonly XNamespace BpmnDiNs = "http://www.omg.org/spec/BPMN/20100524/DI";

    public BpmnValidationResult Validate(string? bpmnXml)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(bpmnXml))
            return BpmnValidationResult.Invalid("empty_xml", "BPMN XML is empty.");

        XDocument doc;
        try { doc = XDocument.Parse(bpmnXml); }
        catch (Exception ex)
        {
            return BpmnValidationResult.Invalid("malformed_xml", $"Not well-formed XML: {ex.Message}");
        }

        var root = doc.Root;
        if (root == null)
            return BpmnValidationResult.Invalid("no_root", "XML has no root element.");

        if (root.Name.LocalName != "definitions")
            return BpmnValidationResult.Invalid("wrong_root", $"Root element is <{root.Name.LocalName}>, expected <definitions>.");

        var processes = root.Descendants().Where(e => e.Name.LocalName == "process").ToList();
        var collaborations = root.Descendants().Where(e => e.Name.LocalName == "collaboration").ToList();

        if (processes.Count == 0 && collaborations.Count == 0)
            return BpmnValidationResult.Invalid("no_process", "No <process> or <collaboration> element found.");

        // Collect all process-element IDs (tasks, events, gateways, subprocesses, flows, lanes, participants).
        var processElementIds = new HashSet<string>(StringComparer.Ordinal);
        int flowNodeCount = 0;
        int sequenceFlowCount = 0;

        foreach (var el in root.Descendants())
        {
            var id = el.Attribute("id")?.Value;
            if (!string.IsNullOrEmpty(id)) processElementIds.Add(id);

            switch (el.Name.LocalName)
            {
                case "task":
                case "userTask":
                case "serviceTask":
                case "manualTask":
                case "scriptTask":
                case "businessRuleTask":
                case "sendTask":
                case "receiveTask":
                case "callActivity":
                case "subProcess":
                case "startEvent":
                case "endEvent":
                case "intermediateThrowEvent":
                case "intermediateCatchEvent":
                case "boundaryEvent":
                case "exclusiveGateway":
                case "parallelGateway":
                case "inclusiveGateway":
                case "complexGateway":
                case "eventBasedGateway":
                    flowNodeCount++;
                    break;
                case "sequenceFlow":
                    sequenceFlowCount++;
                    break;
            }
        }

        if (flowNodeCount == 0)
            return BpmnValidationResult.Invalid("no_flow_nodes", "Process has no tasks, events, or gateways.");

        // Sequence flows: both endpoints must reference existing IDs.
        foreach (var flow in root.Descendants().Where(e => e.Name.LocalName == "sequenceFlow"))
        {
            var src = flow.Attribute("sourceRef")?.Value;
            var tgt = flow.Attribute("targetRef")?.Value;
            if (string.IsNullOrEmpty(src) || !processElementIds.Contains(src))
                errors.Add($"sequenceFlow '{flow.Attribute("id")?.Value}' has missing/invalid sourceRef='{src}'.");
            if (string.IsNullOrEmpty(tgt) || !processElementIds.Contains(tgt))
                errors.Add($"sequenceFlow '{flow.Attribute("id")?.Value}' has missing/invalid targetRef='{tgt}'.");
        }

        // DI integrity: every <BPMNShape bpmnElement="..."> and <BPMNEdge bpmnElement="...">
        // must point to an element we saw in the process tree.
        var diagrams = root.Descendants().Where(e => e.Name.LocalName == "BPMNDiagram").ToList();
        int diShapeCount = 0, diEdgeCount = 0, diOrphanCount = 0;

        foreach (var diagram in diagrams)
        {
            foreach (var shape in diagram.Descendants().Where(e => e.Name.LocalName == "BPMNShape"))
            {
                diShapeCount++;
                var refId = shape.Attribute("bpmnElement")?.Value;
                if (string.IsNullOrEmpty(refId) || !processElementIds.Contains(refId))
                {
                    diOrphanCount++;
                    errors.Add($"BPMNShape '{shape.Attribute("id")?.Value}' references nonexistent bpmnElement='{refId}'.");
                }
            }
            foreach (var edge in diagram.Descendants().Where(e => e.Name.LocalName == "BPMNEdge"))
            {
                diEdgeCount++;
                var refId = edge.Attribute("bpmnElement")?.Value;
                if (string.IsNullOrEmpty(refId) || !processElementIds.Contains(refId))
                {
                    diOrphanCount++;
                    errors.Add($"BPMNEdge '{edge.Attribute("id")?.Value}' references nonexistent bpmnElement='{refId}'.");
                }
            }
        }

        if (diagrams.Count == 0)
            warnings.Add("No <BPMNDiagram> element — the file has a process model but no layout; bpmn-js will auto-layout.");
        else if (diShapeCount == 0)
            warnings.Add("BPMNDiagram present but contains no shapes.");

        if (sequenceFlowCount == 0)
            warnings.Add("No sequence flows — process elements are disconnected.");

        var isValid = errors.Count == 0;
        return new BpmnValidationResult(
            IsValid: isValid,
            Reason: isValid ? "ok" : "structural_errors",
            Errors: errors,
            Warnings: warnings,
            FlowNodeCount: flowNodeCount,
            SequenceFlowCount: sequenceFlowCount,
            DiShapeCount: diShapeCount,
            DiEdgeCount: diEdgeCount,
            DiOrphanCount: diOrphanCount);
    }
}

public interface IBpmnValidator
{
    BpmnValidationResult Validate(string? bpmnXml);
}

public sealed record BpmnValidationResult(
    bool IsValid,
    string Reason,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    int FlowNodeCount,
    int SequenceFlowCount,
    int DiShapeCount,
    int DiEdgeCount,
    int DiOrphanCount)
{
    public static BpmnValidationResult Invalid(string reason, string error) => new(
        IsValid: false, Reason: reason,
        Errors: [error], Warnings: [],
        FlowNodeCount: 0, SequenceFlowCount: 0,
        DiShapeCount: 0, DiEdgeCount: 0, DiOrphanCount: 0);
}
