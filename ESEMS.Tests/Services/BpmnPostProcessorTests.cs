using ESEMS.Web.Services.Bpmn;

namespace ESEMS.Tests.Services;

public class BpmnPostProcessorTests
{
    private readonly BpmnPostProcessor _pp = new();

    [Fact]
    public void SanitizeIds_ReplacesArabicIds_AndRewritesAllRefs()
    {
        // Mirrors real LLM output that broke bpmn-js rendering for the
        // AUTO-* import smoke test: Arabic inside Collaboration / Process /
        // Participant IDs. bpmn-moddle rejects these as "illegal ID".
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<definitions xmlns=""http://www.omg.org/spec/BPMN/20100524/MODEL"" xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI"" xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC"" id=""D1"">
  <collaboration id=""Collaboration_إدارة"">
    <participant id=""Participant_عميل"" name=""عميل"" processRef=""Process_عميل"" />
  </collaboration>
  <process id=""Process_عميل"" isExecutable=""true"">
    <startEvent id=""Start_1"" />
    <sequenceFlow id=""Flow_1"" sourceRef=""Start_1"" targetRef=""Start_1"" />
  </process>
  <bpmndi:BPMNDiagram id=""Diag_1"">
    <bpmndi:BPMNPlane id=""Plane_1"" bpmnElement=""Collaboration_إدارة"">
      <bpmndi:BPMNShape id=""Shape_عميل"" bpmnElement=""Participant_عميل"">
        <dc:Bounds x=""0"" y=""0"" width=""100"" height=""50"" />
      </bpmndi:BPMNShape>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</definitions>";

        var result = _pp.SanitizeIds(xml);

        Assert.True(result.IdsRewritten >= 4, $"Expected ≥4 rewrites, got {result.IdsRewritten}");
        // id attributes must be pure ASCII after sanitize.
        Assert.DoesNotContain("id=\"Collaboration_إدارة", result.BpmnXml);
        Assert.DoesNotContain("id=\"Process_عميل", result.BpmnXml);
        Assert.DoesNotContain("id=\"Participant_عميل", result.BpmnXml);
        Assert.DoesNotContain("id=\"Shape_عميل", result.BpmnXml);
        // processRef / bpmnElement must resolve to the new IDs. BPMN element
        // local names are lowercase, so generated IDs are too.
        Assert.Contains("processRef=\"process_1\"", result.BpmnXml);
        Assert.Contains("bpmnElement=\"collaboration_1\"", result.BpmnXml);
        Assert.Contains("bpmnElement=\"participant_1\"", result.BpmnXml);
    }

    [Fact]
    public void SanitizeIds_AsciiOnlyIds_AreLeftUntouched()
    {
        var xml = @"<?xml version=""1.0""?>
<definitions xmlns=""http://www.omg.org/spec/BPMN/20100524/MODEL"">
  <process id=""Process_1"">
    <startEvent id=""Start_1"" />
  </process>
</definitions>";

        var result = _pp.SanitizeIds(xml);

        Assert.Equal(0, result.IdsRewritten);
        Assert.Equal(xml, result.BpmnXml);
    }

    [Fact]
    public void SanitizeIds_RewritesIncomingOutgoingTextRefs()
    {
        // Flow id is Arabic-clean here, but the <outgoing> text references
        // an Arabic-named sequenceFlow — also unusual, also worth covering.
        var xml = @"<?xml version=""1.0""?>
<definitions xmlns=""http://www.omg.org/spec/BPMN/20100524/MODEL"">
  <process id=""P1"">
    <startEvent id=""S1"">
      <outgoing>Flow_الأول</outgoing>
    </startEvent>
    <endEvent id=""E1"">
      <incoming>Flow_الأول</incoming>
    </endEvent>
    <sequenceFlow id=""Flow_الأول"" sourceRef=""S1"" targetRef=""E1"" />
  </process>
</definitions>";

        var result = _pp.SanitizeIds(xml);

        Assert.Equal(1, result.IdsRewritten);
        Assert.DoesNotContain("الأول", result.BpmnXml);
        // Both the <incoming>/<outgoing> text nodes AND the sourceRef/
        // targetRef attributes must be updated to the new flow id.
        Assert.Contains("<outgoing>sequenceFlow_1</outgoing>", result.BpmnXml);
        Assert.Contains("<incoming>sequenceFlow_1</incoming>", result.BpmnXml);
        Assert.Contains(@"id=""sequenceFlow_1""", result.BpmnXml);
    }

    [Fact]
    public void SanitizeIds_PreservesNameAttributes()
    {
        // Only the *id* is NCName-restricted. Arabic in name= must survive.
        var xml = @"<?xml version=""1.0""?>
<definitions xmlns=""http://www.omg.org/spec/BPMN/20100524/MODEL"">
  <process id=""Process_عميل"" name=""عملية العميل"" />
</definitions>";

        var result = _pp.SanitizeIds(xml);

        Assert.Equal(1, result.IdsRewritten);
        Assert.Contains(@"name=""عملية العميل""", result.BpmnXml);
        Assert.Contains(@"id=""process_1""", result.BpmnXml);
    }
}
