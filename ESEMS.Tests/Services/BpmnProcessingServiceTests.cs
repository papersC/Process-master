using ESEMS.Web.Services.Bpmn;

namespace ESEMS.Tests.Services;

public class BpmnProcessingServiceTests
{
    private readonly BpmnProcessingService _service = new();

    private const string ValidBpmnXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
  xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI""
  xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC""
  xmlns:di=""http://www.omg.org/spec/DD/20100524/DI""
  id=""Definitions_1"" targetNamespace=""http://bpmn.io/schema/bpmn"">
  <bpmn:process id=""Process_1"" isExecutable=""true"">
    <bpmn:startEvent id=""Start_1"">
      <bpmn:outgoing>Flow_1</bpmn:outgoing>
    </bpmn:startEvent>
    <bpmn:userTask id=""Task_1"" name=""Review"">
      <bpmn:incoming>Flow_1</bpmn:incoming>
      <bpmn:outgoing>Flow_2</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:endEvent id=""End_1"">
      <bpmn:incoming>Flow_2</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""Start_1"" targetRef=""Task_1"" />
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_1"" targetRef=""End_1"" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Process_1"">
      <bpmndi:BPMNShape id=""Start_1_di"" bpmnElement=""Start_1"">
        <dc:Bounds x=""100"" y=""100"" width=""36"" height=""36"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_1_di"" bpmnElement=""Task_1"">
        <dc:Bounds x=""200"" y=""78"" width=""100"" height=""80"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""End_1_di"" bpmnElement=""End_1"">
        <dc:Bounds x=""400"" y=""100"" width=""36"" height=""36"" />
      </bpmndi:BPMNShape>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>";

    [Fact]
    public void LooksLikeBpmnXml_ValidXml_ReturnsTrue()
    {
        Assert.True(_service.LooksLikeBpmnXml(ValidBpmnXml));
    }

    [Fact]
    public void LooksLikeBpmnXml_InvalidXml_ReturnsFalse()
    {
        Assert.False(_service.LooksLikeBpmnXml("This is not XML"));
        Assert.False(_service.LooksLikeBpmnXml("<html><body>Not BPMN</body></html>"));
        Assert.False(_service.LooksLikeBpmnXml(""));
        Assert.False(_service.LooksLikeBpmnXml(null!));
    }

    [Fact]
    public void LooksLikeBpmnXml_MissingClosingTag_ReturnsFalse()
    {
        var incomplete = @"<?xml version=""1.0""?><bpmn:definitions><bpmn:process></bpmn:process>";
        Assert.False(_service.LooksLikeBpmnXml(incomplete));
    }

    [Fact]
    public void CleanBpmnXml_RemovesCodeFences()
    {
        var wrapped = "```xml\n" + ValidBpmnXml + "\n```";
        var result = _service.CleanBpmnXml(wrapped);
        Assert.DoesNotContain("```", result);
        Assert.True(_service.LooksLikeBpmnXml(result));
    }

    [Fact]
    public void CleanBpmnXml_FixesXmlDeclaration()
    {
        // Remove XML declaration from valid BPMN
        var noDecl = ValidBpmnXml.Replace(@"<?xml version=""1.0"" encoding=""UTF-8""?>", "").Trim();
        var result = _service.CleanBpmnXml(noDecl);
        Assert.StartsWith("<?xml", result);
    }

    [Fact]
    public void CleanBpmnXml_TrimsTrailingProse()
    {
        var withProse = ValidBpmnXml + "\n\nHere is the explanation of the diagram...";
        var result = _service.CleanBpmnXml(withProse);
        Assert.EndsWith("</bpmn:definitions>", result);
    }

    [Fact]
    public void CleanBpmnXml_SkipsLeadingProse()
    {
        var withLeading = "Here is the BPMN diagram:\n\n" + ValidBpmnXml;
        var result = _service.CleanBpmnXml(withLeading);
        Assert.StartsWith("<?xml", result);
        Assert.True(_service.LooksLikeBpmnXml(result));
    }

    [Fact]
    public void CleanBpmnXml_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _service.CleanBpmnXml(""));
        Assert.Equal(string.Empty, _service.CleanBpmnXml(null!));
        Assert.Equal(string.Empty, _service.CleanBpmnXml("   "));
    }

    [Fact]
    public void CleanBpmnXml_DecodesNumericCharReferences()
    {
        var withEntity = ValidBpmnXml.Replace("Review", "&#x0645;&#x0631;&#x0627;&#x062C;&#x0639;&#x0629;");
        var result = _service.CleanBpmnXml(withEntity);
        Assert.Contains("مراجعة", result);
    }

    [Fact]
    public void CleanBpmnXml_FixesMalformedArabicEntitySequences()
    {
        var malformed = ValidBpmnXml.Replace("Review", "x0645;;&x0631;;&#x0627;;&amp;#x062C;;&#x0639;;&x0629;");
        var result = _service.CleanBpmnXml(malformed);

        Assert.Contains("مراجعة", result);
        Assert.DoesNotContain("x0645", result);
        Assert.DoesNotContain("&x0629", result);
    }

    [Fact]
    public void CleanBpmnXml_PreservesXmlReservedEntities()
    {
        var withReserved = ValidBpmnXml.Replace("Review", "A &#x26; B");
        var result = _service.CleanBpmnXml(withReserved);
        Assert.Contains("&#x26;", result);
    }

    [Fact]
    public void EnsureBpmnDefaultNamespace_InjectsDefaultWhenOnlyPrefixDeclared()
    {
        // Mirrors the real-world AI output that broke bpmn-js rendering:
        // <definitions xmlns:bpmn="..."> (no default xmlns), with unprefixed
        // children that end up in no-namespace until we normalize.
        var broken = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"" id=""D1"">
  <process id=""P1"" isExecutable=""true"">
    <startEvent id=""S1"" />
    <bpmn:userTask id=""T1"" name=""Review"" />
  </process>
</definitions>";

        var result = BpmnProcessingService.EnsureBpmnDefaultNamespace(broken);

        Assert.Contains("xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"", result);
        // Prefix declaration must still be present — mixed prefix/unprefixed
        // children are valid as long as both resolve to the same URI.
        Assert.Contains("xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"", result);
    }

    [Fact]
    public void EnsureBpmnDefaultNamespace_PrefixedRoot_IsUntouched()
    {
        // <bpmn:definitions ...> — the document consistently uses the bpmn:
        // prefix, no injection needed (and injecting would change semantics).
        var result = BpmnProcessingService.EnsureBpmnDefaultNamespace(ValidBpmnXml);
        Assert.Equal(ValidBpmnXml, result);
    }

    [Fact]
    public void EnsureBpmnDefaultNamespace_ExistingDefault_IsUntouched()
    {
        var alreadyHasDefault = @"<?xml version=""1.0""?>
<definitions xmlns=""http://www.omg.org/spec/BPMN/20100524/MODEL"" id=""D1"">
  <process id=""P1"" />
</definitions>";
        var result = BpmnProcessingService.EnsureBpmnDefaultNamespace(alreadyHasDefault);
        Assert.Equal(alreadyHasDefault, result);
    }

    [Fact]
    public void CleanBpmnXml_InjectsDefaultNamespaceForUnprefixedRoot()
    {
        var broken = @"<definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"" id=""D1"">
  <process id=""P1"" isExecutable=""true"">
    <bpmn:userTask id=""T1"" name=""Review"" />
  </process>
</bpmn:definitions>";

        var result = _service.CleanBpmnXml(broken);

        Assert.Contains("xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"", result);
    }

    [Fact]
    public void RepairMissingSequenceFlows_AddsMissingFlows()
    {
        // BPMN with incoming/outgoing references but no sequenceFlow definitions
        var bpmn = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
  xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI""
  id=""D1"" targetNamespace=""http://bpmn.io/schema/bpmn"">
  <bpmn:process id=""P1"" isExecutable=""true"">
    <bpmn:startEvent id=""S1"">
      <bpmn:outgoing>F1</bpmn:outgoing>
    </bpmn:startEvent>
    <bpmn:endEvent id=""E1"">
      <bpmn:incoming>F1</bpmn:incoming>
    </bpmn:endEvent>
  </bpmn:process>
</bpmn:definitions>";

        var result = BpmnProcessingService.RepairMissingSequenceFlows(bpmn);
        Assert.Contains(@"<bpmn:sequenceFlow id=""F1"" sourceRef=""S1"" targetRef=""E1""", result);
    }

    [Fact]
    public void RepairGatewayFlowLabels_AddsYesNoLabels()
    {
        var bpmn = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
  id=""D1"" targetNamespace=""http://bpmn.io/schema/bpmn"">
  <bpmn:process id=""P1"" isExecutable=""true"">
    <bpmn:exclusiveGateway id=""GW1"">
      <bpmn:outgoing>F1</bpmn:outgoing>
      <bpmn:outgoing>F2</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    <bpmn:sequenceFlow id=""F1"" sourceRef=""GW1"" targetRef=""T1"" />
    <bpmn:sequenceFlow id=""F2"" sourceRef=""GW1"" targetRef=""T2"" />
  </bpmn:process>
</bpmn:definitions>";

        var result = BpmnProcessingService.RepairGatewayFlowLabels(bpmn);
        Assert.Contains(@"name=""Yes""", result);
        Assert.Contains(@"name=""No""", result);
    }

    [Fact]
    public void ApplyBpmnColors_AddsColorAttributes()
    {
        var result = BpmnProcessingService.ApplyBpmnColors(ValidBpmnXml);
        // Start event should get green
        Assert.Contains(@"bioc:fill=""#E8F5E9""", result);
        Assert.Contains(@"bioc:stroke=""#4CAF50""", result);
        // User task should get blue
        Assert.Contains(@"bioc:fill=""#BBDEFB""", result);
        // End event should get red
        Assert.Contains(@"bioc:fill=""#FFEBEE""", result);
        // Should add bioc namespace
        Assert.Contains("xmlns:bioc", result);
    }

    [Fact]
    public void CleanBpmnXml_FullPipeline_ProducesValidOutput()
    {
        // Run the full CleanBpmnXml pipeline and verify output is valid
        var result = _service.CleanBpmnXml(ValidBpmnXml);
        Assert.True(_service.LooksLikeBpmnXml(result));
        // Should have colors applied
        Assert.Contains("bioc:fill", result);
        // Running clean again should produce same result (idempotent at pipeline level)
        var result2 = _service.CleanBpmnXml(result);
        Assert.True(_service.LooksLikeBpmnXml(result2));
    }

    // ═══════════════════════════════════════════════════════════════
    // Regression tests for the TryParse defense-in-depth fixes in
    // BpmnProcessingService. The previous int.Parse/Convert.ToInt32
    // could crash the whole clean pass on malformed AI output with
    // huge coordinate values or out-of-range character references.
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CleanBpmnXml_HexEntityOverflow_DoesNotThrow()
    {
        // A hex entity outside the Unicode plane (> 0x10FFFF) used to
        // throw ArgumentOutOfRangeException inside char.ConvertFromUtf32.
        var bad = ValidBpmnXml.Replace(
            "name=\"Review\"",
            "name=\"&#xFFFFFFFF;&#xFF;\"");
        var ex = Record.Exception(() => _service.CleanBpmnXml(bad));
        Assert.Null(ex);
    }

    [Fact]
    public void CleanBpmnXml_DecimalEntityOverflow_DoesNotThrow()
    {
        // Decimal entity overflow from malformed AI output
        var bad = ValidBpmnXml.Replace(
            "name=\"Review\"",
            "name=\"&#99999999999;&#42;\"");
        var ex = Record.Exception(() => _service.CleanBpmnXml(bad));
        Assert.Null(ex);
    }

    [Fact]
    public void CleanBpmnXml_HexEntity_DecodesValidArabicLetter()
    {
        // Happy path: decoding a real Arabic entity still works after the
        // TryParse wrapping. U+0627 = ا (alef)
        var input = ValidBpmnXml.Replace(
            "name=\"Review\"",
            "name=\"&#x627;&#x644;\""); // اا
        var result = _service.CleanBpmnXml(input);
        Assert.Contains("ا", result);
    }

    [Fact]
    public void CleanBpmnXml_DecimalEntity_DecodesValidArabicLetter()
    {
        // Decimal form of the same alef character (U+0627 = 1575)
        var input = ValidBpmnXml.Replace(
            "name=\"Review\"",
            "name=\"&#1575;\"");
        var result = _service.CleanBpmnXml(input);
        Assert.Contains("ا", result);
    }

    [Fact]
    public void CleanBpmnXml_XmlReservedHexEntity_IsPreserved()
    {
        // XML-reserved codepoints like &#x3C; (<) should be left as-is,
        // not decoded, or we'd break the XML structure.
        var input = ValidBpmnXml.Replace(
            "name=\"Review\"",
            "name=\"A&#x3C;B\"");
        var result = _service.CleanBpmnXml(input);
        // Reserved entity should survive
        Assert.Contains("&#x3C;", result);
    }

    [Fact]
    public void CleanBpmnXml_RtlMessageFlow_WithLargeCoordinate_DoesNotThrow()
    {
        // RepairRtlMessageFlows parses x/y/w/h from BPMNShape bounds.
        // A huge value used to crash the whole clean pass.
        var bad = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
  xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI""
  xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC"">
  <bpmn:collaboration id=""Col_1"">
    <bpmn:messageFlow id=""MsgFlow_1"" name=""تقديم""/>
  </bpmn:collaboration>
  <bpmndi:BPMNDiagram>
    <bpmndi:BPMNPlane bpmnElement=""Col_1"">
      <bpmndi:BPMNShape id=""S1_di"" bpmnElement=""S1"">
        <dc:Bounds x=""99999999999999"" y=""200"" width=""150"" height=""80"" />
      </bpmndi:BPMNShape>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>";
        var ex = Record.Exception(() => _service.CleanBpmnXml(bad));
        Assert.Null(ex);
    }

    [Fact]
    public void CleanBpmnXml_DiagonalWaypointParse_DoesNotThrow()
    {
        // RepairDiagonalWaypoints parses x/y pairs. Oversized values used
        // to throw OverflowException inside the Select.
        var bad = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
  xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI""
  xmlns:di=""http://www.omg.org/spec/DD/20100524/DI"">
  <bpmndi:BPMNDiagram>
    <bpmndi:BPMNPlane>
      <bpmndi:BPMNEdge id=""E1_di"" bpmnElement=""Flow_1"">
        <di:waypoint x=""99999999999"" y=""100"" />
        <di:waypoint x=""200"" y=""99999999999"" />
      </bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>";
        var ex = Record.Exception(() => _service.CleanBpmnXml(bad));
        Assert.Null(ex);
    }

    [Fact]
    public void CleanBpmnXml_NullInput_ReturnsEmpty()
    {
        // Defensive: null input should not throw
        var ex = Record.Exception(() => _service.CleanBpmnXml(null!));
        Assert.Null(ex);
    }

    [Fact]
    public void CleanBpmnXml_EmptyStringInput_ReturnsNonNull()
    {
        var result = _service.CleanBpmnXml("");
        Assert.NotNull(result);
    }

    [Fact]
    public void LooksLikeBpmnXml_NullInput_NoThrow()
    {
        Assert.False(_service.LooksLikeBpmnXml(null!));
    }

    [Fact]
    public void LooksLikeBpmnXml_PlainText_ReturnsFalse()
    {
        Assert.False(_service.LooksLikeBpmnXml("hello world"));
    }

    [Fact]
    public void LooksLikeBpmnXml_HtmlWithoutDefinitions_ReturnsFalse()
    {
        Assert.False(_service.LooksLikeBpmnXml("<html><body>nope</body></html>"));
    }
}
