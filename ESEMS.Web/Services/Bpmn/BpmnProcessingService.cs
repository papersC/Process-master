using System.Text.RegularExpressions;

namespace ESEMS.Web.Services.Bpmn;

public class BpmnProcessingService : IBpmnProcessingService
{
    public bool LooksLikeBpmnXml(string text)
    {
        var t = (text ?? string.Empty).TrimStart();
        return (t.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("<bpmn:definitions", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("<definitions", StringComparison.OrdinalIgnoreCase)
               || t.Contains("<bpmn:definitions", StringComparison.OrdinalIgnoreCase))
               && t.Contains("</bpmn:definitions>", StringComparison.OrdinalIgnoreCase);
    }

    public string CleanBpmnXml(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var text = raw.Trim();
        text = text.Replace("\r\n", "\n");

        // Strip ALL ``` fences (```xml, ```bpmn, ```mermaid, etc.)
        var lines = text.Split('\n').ToList();
        lines.RemoveAll(l => l.Trim().StartsWith("```", StringComparison.Ordinal));
        text = string.Join("\n", lines).Trim();

        // Find the XML start — skip any prose before the actual XML
        var candidateLines = text.Split('\n');
        int startIdx = -1;
        for (var i = 0; i < candidateLines.Length; i++)
        {
            var l = candidateLines[i].TrimStart();
            if (l.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
                || l.StartsWith("<bpmn:definitions", StringComparison.OrdinalIgnoreCase)
                || l.StartsWith("<definitions", StringComparison.OrdinalIgnoreCase))
            {
                startIdx = i;
                break;
            }
        }

        if (startIdx >= 0)
            text = string.Join("\n", candidateLines.Skip(startIdx)).Trim();

        // Trim any trailing prose after </bpmn:definitions> or </definitions>
        var closingTag = "</bpmn:definitions>";
        var closeIdx = text.LastIndexOf(closingTag, StringComparison.OrdinalIgnoreCase);
        if (closeIdx < 0)
        {
            closingTag = "</definitions>";
            closeIdx = text.LastIndexOf(closingTag, StringComparison.OrdinalIgnoreCase);
        }
        if (closeIdx >= 0)
            text = text.Substring(0, closeIdx + closingTag.Length);

        // Fix common AI mistakes: missing XML declaration
        if (!text.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
            && (text.StartsWith("<bpmn:definitions", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("<definitions", StringComparison.OrdinalIgnoreCase)))
        {
            text = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + text;
        }

        text = EnsureBpmnDefaultNamespace(text);

        // Run normalization + decode in a loop to handle chained/nested malformed entities
        var xmlReserved = new HashSet<int> { 0x22, 0x26, 0x27, 0x3C, 0x3E };
        for (int pass = 0; pass < 3; pass++)
        {
            var prev = text;
            text = NormalizeMalformedNumericEntities(text);

            // Decode XML numeric character references (&#xHHHH; and &#DDDD;) to Unicode.
            // Malformed AI output can include codepoints larger than int.MaxValue
            // or beyond the Unicode plane (> 0x10FFFF). Use TryParse + range
            // check to return the literal reference unchanged on bad input
            // rather than crashing the whole BPMN cleaning pass.
            text = Regex.Replace(text, @"(?i)&#x([0-9a-f]+);?", m =>
            {
                if (!int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out var code))
                    return m.Value;
                if (code < 0 || code > 0x10FFFF) return m.Value;
                return xmlReserved.Contains(code) ? m.Value : char.ConvertFromUtf32(code);
            });
            text = Regex.Replace(text, @"&#(\d+);?", m =>
            {
                if (!int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out var code))
                    return m.Value;
                if (code < 0 || code > 0x10FFFF) return m.Value;
                return xmlReserved.Contains(code) ? m.Value : char.ConvertFromUtf32(code);
            });

            if (text == prev) break;
        }

        // Some malformed AI outputs leave ASCII semicolons between decoded Arabic letters.
        text = Regex.Replace(text, @"(?<=[\u0600-\u06FF]);+(?=[\u0600-\u06FF])", string.Empty);
        text = Regex.Replace(text, @"(?<=[\u0600-\u06FF]);+(?=""|<)", string.Empty);

        // Repair missing sequenceFlow elements
        text = RepairMissingSequenceFlows(text);

        // Add missing Yes/No labels on gateway outgoing flows
        text = RepairGatewayFlowLabels(text);

        // Fix diagonal arrows — convert to orthogonal (horizontal + vertical)
        text = RepairDiagonalWaypoints(text);

        // Fix message flow waypoints for RTL Arabic diagrams
        text = RepairRtlMessageFlows(text);

        // Apply ISO/best-practice colors to BPMN elements
        text = ApplyBpmnColors(text);

        return text;
    }

    // Some LLM outputs declare `xmlns:bpmn="..."` on <definitions> but leave
    // many children unprefixed (<process>, <task>, <sequenceFlow>, DI
    // elements). Those unprefixed children land in no-namespace, while our
    // post-processor promotes generic <task> to <bpmn:userTask> in the BPMN
    // namespace — producing a mixed-namespace doc that bpmn-js rejects with
    // "Error loading diagram". Injecting a default xmlns equal to the BPMN
    // URI fixes both problems at once: unprefixed children resolve to BPMN,
    // and prefixed children stay in BPMN (same URI), so the whole document
    // is consistently in the BPMN namespace.
    internal static string EnsureBpmnDefaultNamespace(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        const string bpmnUri = "http://www.omg.org/spec/BPMN/20100524/MODEL";

        // Locate the root <definitions ...> or <bpmn:definitions ...>.
        var rootMatch = Regex.Match(text, @"<(bpmn:)?definitions\b([^>]*)>",
            RegexOptions.IgnoreCase);
        if (!rootMatch.Success) return text;

        var openTag = rootMatch.Value;

        // If a default namespace is already declared on the root, trust the author.
        if (Regex.IsMatch(openTag, @"\sxmlns\s*="))
            return text;

        // Only auto-inject when the root is the UNPREFIXED <definitions>. If
        // the document consistently uses the bpmn: prefix everywhere (root +
        // children), no fix is needed and we must not add an unrelated default.
        if (openTag.StartsWith("<bpmn:definitions", StringComparison.OrdinalIgnoreCase))
            return text;

        // Insert xmlns="<BPMN_URI>" right after "<definitions" so subsequent
        // attributes (targetNamespace, xsi:schemaLocation, etc.) keep their order.
        var newOpen = openTag.Insert("<definitions".Length, $" xmlns=\"{bpmnUri}\"");
        return string.Concat(
            text.AsSpan(0, rootMatch.Index),
            newOpen,
            text.AsSpan(rootMatch.Index + openTag.Length));
    }

    private static string NormalizeMalformedNumericEntities(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Fix common double-encoding from AI or Visio like &amp;#x62C;
        text = text.Replace("&amp;#", "&#");
        text = text.Replace("&amp;x", "&#x");

        // Remove stray semicolons inserted before the next entity.
        text = Regex.Replace(text, @";\s*(&#x?[0-9a-f]+;?)", "$1", RegexOptions.IgnoreCase);

        // Normalize malformed entity variants such as &x62C;, #x62C;, or x62C;.
        text = Regex.Replace(text, @"(?i)&x([0-9a-f]{2,6});?", "&#x$1;");
        text = Regex.Replace(text, @"(?i)(?<!&)#x([0-9a-f]{2,6});?", "&#x$1;");
        text = Regex.Replace(text, @"(?i)(?<![&#A-Za-z0-9])x([0-9a-f]{2,6});?", "&#x$1;");
        // Catch bare x after semicolon (chained entities like ;x62C;x627;)
        text = Regex.Replace(text, @"(?i)(?<=;)x([0-9a-f]{2,6});?", "&#x$1;");

        // Normalize malformed decimal entities such as &1605; or #1605;.
        text = Regex.Replace(text, @"&([0-9]{2,7});?", "&#$1;");
        text = Regex.Replace(text, @"(?<!&)#([0-9]{2,7});?", "&#$1;");

        return text;
    }

    internal static string RepairMissingSequenceFlows(string xml)
    {
        // Collect all flow IDs referenced in incoming/outgoing
        var referencedFlows = new HashSet<string>();
        foreach (Match m in Regex.Matches(xml, @"<bpmn:(?:incoming|outgoing)>([^<]+)</bpmn:(?:incoming|outgoing)>"))
            referencedFlows.Add(m.Groups[1].Value);

        if (referencedFlows.Count == 0) return xml;

        // Collect flow IDs that already have sequenceFlow definitions
        var definedFlows = new HashSet<string>();
        foreach (Match m in Regex.Matches(xml, @"<bpmn:sequenceFlow\s+id=""([^""]+)"""))
            definedFlows.Add(m.Groups[1].Value);

        // Find missing flows
        var missingFlows = referencedFlows.Except(definedFlows).ToList();
        if (missingFlows.Count == 0) return xml;

        // Build a map: flowId -> (sourceElement, targetElement) from incoming/outgoing tags
        var flowSource = new Dictionary<string, string>();
        var flowTarget = new Dictionary<string, string>();

        foreach (Match elemMatch in Regex.Matches(xml,
            @"<bpmn:(?:startEvent|endEvent|userTask|serviceTask|exclusiveGateway)\s+id=""([^""]+)""[^>]*>.*?</bpmn:(?:startEvent|endEvent|userTask|serviceTask|exclusiveGateway)>",
            RegexOptions.Singleline))
        {
            var elemId = elemMatch.Groups[1].Value;
            foreach (Match outM in Regex.Matches(elemMatch.Value, @"<bpmn:outgoing>([^<]+)</bpmn:outgoing>"))
                flowSource[outM.Groups[1].Value] = elemId;
            foreach (Match inM in Regex.Matches(elemMatch.Value, @"<bpmn:incoming>([^<]+)</bpmn:incoming>"))
                flowTarget[inM.Groups[1].Value] = elemId;
        }

        // Generate missing sequenceFlow elements
        var flowElements = new List<string>();
        foreach (var flowId in missingFlows.OrderBy(f => f))
        {
            if (flowSource.TryGetValue(flowId, out var src) && flowTarget.TryGetValue(flowId, out var tgt))
            {
                flowElements.Add($"    <bpmn:sequenceFlow id=\"{flowId}\" sourceRef=\"{src}\" targetRef=\"{tgt}\" />");
            }
        }

        if (flowElements.Count == 0) return xml;

        // Insert before </bpmn:process>
        var insertPoint = xml.IndexOf("</bpmn:process>", StringComparison.OrdinalIgnoreCase);
        if (insertPoint < 0) return xml;

        var flowBlock = string.Join("\n", flowElements) + "\n";
        xml = xml.Insert(insertPoint, flowBlock + "    ");

        return xml;
    }

    internal static string RepairGatewayFlowLabels(string xml)
    {
        // Detect if diagram uses Arabic text (check name attributes for Arabic chars)
        bool isArabic = Regex.IsMatch(xml, @"name=""[^""]*[\u0600-\u06FF]");

        // Find all exclusive gateways and their outgoing flow IDs
        var gatewayOutgoing = new Dictionary<string, List<string>>();
        foreach (Match gw in Regex.Matches(xml,
            @"<bpmn:exclusiveGateway\s+id=""([^""]+)""[^>]*>.*?</bpmn:exclusiveGateway>",
            RegexOptions.Singleline))
        {
            var gwId = gw.Groups[1].Value;
            var outFlows = Regex.Matches(gw.Value, @"<bpmn:outgoing>([^<]+)</bpmn:outgoing>")
                .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            if (outFlows.Count == 2)
                gatewayOutgoing[gwId] = outFlows;
        }

        if (gatewayOutgoing.Count == 0) return xml;

        var yesLabel = isArabic ? "موافق" : "Yes";
        var noLabel = isArabic ? "مرفوض" : "No";

        foreach (var kvp in gatewayOutgoing)
        {
            var flowIds = kvp.Value;
            var labels = new[] { yesLabel, noLabel };
            int labelIdx = 0;
            foreach (var flowId in flowIds)
            {
                var flowPattern = $@"<bpmn:sequenceFlow\s+id=""{Regex.Escape(flowId)}""";
                var flowMatch = Regex.Match(xml, flowPattern);
                if (flowMatch.Success)
                {
                    var fullTag = xml.Substring(flowMatch.Index, xml.IndexOf("/>", flowMatch.Index) - flowMatch.Index + 2);
                    if (!fullTag.Contains("name=\""))
                    {
                        var replacement = fullTag.Replace("/>", $" name=\"{labels[labelIdx]}\" />");
                        xml = xml.Substring(0, flowMatch.Index) + replacement + xml.Substring(flowMatch.Index + fullTag.Length);
                    }
                    else if (isArabic)
                    {
                        // Replace English Yes/No/Approved/Rejected with Arabic equivalents
                        var updated = Regex.Replace(fullTag, @"name=""(Yes|Approved)""", $@"name=""{yesLabel}""", RegexOptions.IgnoreCase);
                        updated = Regex.Replace(updated, @"name=""(No|Rejected)""", $@"name=""{noLabel}""", RegexOptions.IgnoreCase);
                        if (updated != fullTag)
                            xml = xml.Substring(0, flowMatch.Index) + updated + xml.Substring(flowMatch.Index + fullTag.Length);
                    }
                }
                labelIdx++;
            }
        }

        // Also fix any remaining Yes/No/Approved/Rejected labels on sequenceFlows in Arabic diagrams
        if (isArabic)
        {
            xml = Regex.Replace(xml, @"(<bpmn:sequenceFlow[^>]*name="")(Yes|Approved)("")", $"$1{yesLabel}$3", RegexOptions.IgnoreCase);
            xml = Regex.Replace(xml, @"(<bpmn:sequenceFlow[^>]*name="")(No|Rejected)("")", $"$1{noLabel}$3", RegexOptions.IgnoreCase);
        }

        return xml;
    }

    internal static string RepairDiagonalWaypoints(string xml)
    {
        return Regex.Replace(xml,
            @"(<bpmndi:BPMNEdge[^>]*>)(.*?)(</bpmndi:BPMNEdge>)",
            match =>
            {
                var open = match.Groups[1].Value;
                var content = match.Groups[2].Value;
                var close = match.Groups[3].Value;

                var wpMatches = Regex.Matches(content, @"<di:waypoint\s+x=""(\d+)""\s+y=""(\d+)""\s*/>");
                if (wpMatches.Count < 2) return match.Value;

                // TryParse guards against overflow on very large coordinate
                // values from malformed AI output. We drop unparseable points
                // rather than throwing and aborting the whole repair pass.
                var points = wpMatches.Cast<Match>()
                    .Select(m =>
                    {
                        int.TryParse(m.Groups[1].Value, out var x);
                        int.TryParse(m.Groups[2].Value, out var y);
                        return (x, y);
                    })
                    .ToList();

                bool hasDiagonal = false;
                for (int i = 1; i < points.Count; i++)
                {
                    if (points[i].x != points[i - 1].x && points[i].y != points[i - 1].y)
                    {
                        hasDiagonal = true;
                        break;
                    }
                }

                if (!hasDiagonal) return match.Value;

                var newPoints = new List<(int x, int y)> { points[0] };
                for (int i = 1; i < points.Count; i++)
                {
                    var prev = newPoints[^1];
                    var curr = points[i];
                    if (prev.x != curr.x && prev.y != curr.y)
                    {
                        int midX = (prev.x + curr.x) / 2;
                        newPoints.Add((midX, prev.y));
                        newPoints.Add((midX, curr.y));
                    }
                    newPoints.Add(curr);
                }

                var indent = "        ";
                var wpXml = string.Join("\n", newPoints.Select(p =>
                    $"{indent}<di:waypoint x=\"{p.x}\" y=\"{p.y}\" />"));

                var otherContent = Regex.Replace(content, @"<di:waypoint[^/]*/>\s*", "").Trim();
                var newContent = "\n" + wpXml + "\n" +
                    (string.IsNullOrWhiteSpace(otherContent) ? "" : "      " + otherContent + "\n") +
                    "      ";

                return open + newContent + close;
            },
            RegexOptions.Singleline);
    }

    internal static string RepairRtlMessageFlows(string xml)
    {
        // Only apply to Arabic/RTL diagrams
        if (!Regex.IsMatch(xml, @"name=""[^""]*[\u0600-\u06FF]"))
            return xml;

        // Collect element positions: id -> (x, y, w, h)
        var positions = new Dictionary<string, (int x, int y, int w, int h)>();
        foreach (Match m in Regex.Matches(xml,
            @"<bpmndi:BPMNShape\s+id=""[^""]*""\s+bpmnElement=""([^""]+)""[^>]*>\s*<dc:Bounds\s+x=""(\d+)""\s+y=""(\d+)""\s+width=""(\d+)""\s+height=""(\d+)""",
            RegexOptions.Singleline))
        {
            // TryParse — overflow-safe. Bad coordinate strings are skipped
            // silently; we'd rather lose a shape than abort the whole pass.
            if (int.TryParse(m.Groups[2].Value, out var px) &&
                int.TryParse(m.Groups[3].Value, out var py) &&
                int.TryParse(m.Groups[4].Value, out var pw) &&
                int.TryParse(m.Groups[5].Value, out var ph))
            {
                positions[m.Groups[1].Value] = (px, py, pw, ph);
            }
        }

        // Fix each messageFlow BPMNEdge
        xml = Regex.Replace(xml,
            @"(<bpmndi:BPMNEdge\s+id=""([^""]*)""\s+bpmnElement=""(MsgFlow[^""]*)""\s*>)(.*?)(</bpmndi:BPMNEdge>)",
            match =>
            {
                var open = match.Groups[1].Value;
                var edgeId = match.Groups[2].Value;
                var flowId = match.Groups[3].Value;
                var content = match.Groups[4].Value;
                var close = match.Groups[5].Value;

                // Find the messageFlow source/target refs
                var flowMatch = Regex.Match(xml,
                    $@"<bpmn:messageFlow\s+id=""{Regex.Escape(flowId)}""[^>]*sourceRef=""([^""]+)""[^>]*targetRef=""([^""]+)""");
                if (!flowMatch.Success) return match.Value;

                var sourceId = flowMatch.Groups[1].Value;
                var targetId = flowMatch.Groups[2].Value;

                if (!positions.ContainsKey(sourceId) || !positions.ContainsKey(targetId))
                    return match.Value;

                var src = positions[sourceId];
                var tgt = positions[targetId];

                // Calculate proper RTL message flow waypoints (vertical dashed line)
                int srcCenterX = src.x + src.w / 2;
                int tgtCenterX = tgt.x + tgt.w / 2;
                int srcBottomY = src.y + src.h;
                int tgtTopY = tgt.y;

                // For message flow from customer pool (top) to start event (below):
                // Use target's X center for alignment (vertical drop)
                string wpXml;
                if (src.y < tgt.y)
                {
                    // Source above target — drop down from source at target's x
                    wpXml = $@"
        <di:waypoint x=""{tgtCenterX}"" y=""{srcBottomY}"" />
        <di:waypoint x=""{tgtCenterX}"" y=""{tgtTopY}"" />
      ";
                }
                else
                {
                    // Source below target — go up from source at source's x
                    int srcTopY = src.y;
                    int tgtBottomY = tgt.y + tgt.h;
                    wpXml = $@"
        <di:waypoint x=""{srcCenterX}"" y=""{srcTopY}"" />
        <di:waypoint x=""{srcCenterX}"" y=""{tgtBottomY}"" />
      ";
                }

                // Preserve any label within the edge
                var labelMatch = Regex.Match(content, @"<bpmndi:BPMNLabel>.*?</bpmndi:BPMNLabel>", RegexOptions.Singleline);
                var labelXml = labelMatch.Success ? "\n      " + labelMatch.Value : "";

                return open + wpXml + labelXml + "\n    " + close;
            },
            RegexOptions.Singleline);

        return xml;
    }

    internal static string ApplyBpmnColors(string xml)
    {
        // Add bioc namespace to definitions if not present
        if (!xml.Contains("xmlns:bioc"))
        {
            xml = Regex.Replace(xml,
                @"(<bpmn:definitions\s)",
                "$1xmlns:bioc=\"http://bpmn.io/schema/bpmn/biocolor/1.0\" ");
        }

        // Build element type map: elementId -> type
        var elementTypes = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(xml,
            @"<bpmn:(startEvent|endEvent|userTask|serviceTask|exclusiveGateway|subProcess)\s+id=""([^""]+)"""))
        {
            elementTypes[m.Groups[2].Value] = m.Groups[1].Value;
        }

        // Color definitions (fill, stroke)
        var colors = new Dictionary<string, (string fill, string stroke)>
        {
            { "startEvent",       ("#E8F5E9", "#4CAF50") },
            { "endEvent",         ("#FFEBEE", "#F44336") },
            { "userTask",         ("#BBDEFB", "#1E88E5") },
            { "serviceTask",      ("#FFE0B2", "#FB8C00") },
            { "exclusiveGateway", ("#FFF9C4", "#F9A825") },
            { "subProcess",       ("#E1BEE7", "#8E24AA") },
        };

        // Apply colors to BPMNShape elements
        xml = Regex.Replace(xml,
            @"(<bpmndi:BPMNShape\s+id=""([^""]+)""\s+bpmnElement=""([^""]+)"")",
            match =>
            {
                var fullMatch = match.Groups[0].Value;
                var bpmnElement = match.Groups[3].Value;

                if (fullMatch.Contains("bioc:fill")) return fullMatch;

                if (elementTypes.TryGetValue(bpmnElement, out var elementType) &&
                    colors.TryGetValue(elementType, out var color))
                {
                    return $"{fullMatch} bioc:fill=\"{color.fill}\" bioc:stroke=\"{color.stroke}\"";
                }

                return fullMatch;
            });

        return xml;
    }
}
