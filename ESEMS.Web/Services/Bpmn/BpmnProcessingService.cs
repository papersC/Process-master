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

    // ──────────────────────────────────────────────────────────────────────
    // Enhance-drawing pipeline
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// "Enhance Drawing" — runs the full clean-up (diagonal repair, RTL
    /// repair, colours) and then a layout pass that re-routes edges with
    /// orthogonal waypoints anchored to shape boundaries, centres edge
    /// labels on the new midpoint, and grows task shapes that visibly
    /// can't fit their label text. Returns the enhanced XML, or the input
    /// unchanged when no shapes are found (e.g. when the XML is invalid).
    /// </summary>
    public string EnhanceBpmnLayout(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return xml;

        // Start from the existing repair pipeline so we don't re-implement
        // anything CleanBpmnXml already handles.
        var text = CleanBpmnXml(xml);

        // Collect every BPMNShape's bounding box, keyed by the bpmnElement
        // id it represents. Without this map we can't compute where edges
        // should start/end, can't centre labels on edges, and can't tell
        // whether a task shape is large enough for its label.
        var shapeBounds = CollectShapeBounds(text);
        if (shapeBounds.Count == 0)
            return text;

        // Read every element id → label so we can size task shapes by their
        // text and centre edge labels by source/target proximity.
        var elementNames = CollectElementNames(text);

        text = ResizeTaskShapesToFitLabels(text, shapeBounds, elementNames);
        // shapeBounds may now be stale after resizing; rebuild before we use
        // it to route edges.
        shapeBounds = CollectShapeBounds(text);

        // Type map — used to filter obstacles. Pools (bpmn:Participant) and
        // lanes (bpmn:Lane) are CONTAINERS, not obstacles: a sequence flow
        // crossing a lane border is expected and correct, while a flow
        // crossing a Task is the bug we're trying to fix.
        var shapeTypes = CollectShapeTypes(text);

        // Detect the writing direction so the overlap resolver pushes
        // shapes apart in a direction that matches the reading flow.
        // Arabic content (Unicode block U+0600–U+06FF on any name) →
        // right-to-left layout; everything else is left-to-right.
        var isRtl = ContainsArabic(text);

        // Push overlapping shapes apart. Must run AFTER ResizeTaskShapes
        // (which can create new overlaps by growing a task) and BEFORE
        // ReRoute (which uses the final positions as obstacles). Rebuild
        // bounds afterwards so the router sees the resolved layout.
        text = ResolveShapeOverlaps(text, shapeBounds, shapeTypes, isRtl);
        shapeBounds = CollectShapeBounds(text);

        text = ReRouteSequenceFlowsOrthogonally(text, shapeBounds, shapeTypes);
        text = CenterEdgeLabels(text, shapeBounds);

        return text;
    }

    /// <summary>
    /// True if any name/text attribute in the document contains a character
    /// from the Arabic Unicode block. Same heuristic the existing
    /// <c>RepairGatewayFlowLabels</c> / <c>RepairRtlMessageFlows</c> use
    /// — language detection by content sample.
    /// </summary>
    internal static bool ContainsArabic(string xml)
    {
        return Regex.IsMatch(xml, @"[؀-ۿݐ-ݿࢠ-ࣿ]");
    }

    // Visibility matches the helper methods that take it (internal static)
    // — sealed record so tests can construct fixtures directly without
    // round-tripping XML.
    internal sealed record ShapeBox(double X, double Y, double W, double H)
    {
        public double Cx => X + W / 2;
        public double Cy => Y + H / 2;
        public double Right => X + W;
        public double Bottom => Y + H;
    }

    // BPMNShape → BPMN element id mapping (the `bpmnElement` attr). Built
    // once and reused across the enhancement passes; the keys match the
    // sourceRef / targetRef values on every edge.
    private static Dictionary<string, ShapeBox> CollectShapeBounds(string xml)
    {
        var map = new Dictionary<string, ShapeBox>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(xml,
            @"<bpmndi:BPMNShape[^>]*bpmnElement=""([^""]+)""[^>]*>\s*<dc:Bounds\s+x=""(-?\d+(?:\.\d+)?)""\s+y=""(-?\d+(?:\.\d+)?)""\s+width=""(\d+(?:\.\d+)?)""\s+height=""(\d+(?:\.\d+)?)""",
            RegexOptions.Singleline))
        {
            if (double.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                && double.TryParse(m.Groups[3].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y)
                && double.TryParse(m.Groups[4].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w)
                && double.TryParse(m.Groups[5].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h))
            {
                map[m.Groups[1].Value] = new ShapeBox(x, y, w, h);
            }
        }
        return map;
    }

    // BPMN element id → display name. We strip whitespace runs to a single
    // space so the label-width heuristic in
    // <see cref="ResizeTaskShapesToFitLabels"/> doesn't over-count.
    private static Dictionary<string, string> CollectElementNames(string xml)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(xml,
            @"<bpmn:(?:userTask|serviceTask|task|manualTask|sendTask|receiveTask|scriptTask|businessRuleTask|startEvent|endEvent|intermediateThrowEvent|intermediateCatchEvent|exclusiveGateway|parallelGateway|inclusiveGateway|eventBasedGateway|subProcess)\s+id=""([^""]+)""[^>]*\sname=""([^""]*)"""))
        {
            map[m.Groups[1].Value] = Regex.Replace(m.Groups[2].Value, @"\s+", " ").Trim();
        }
        return map;
    }

    // BPMN element id → its concrete BPMN type ("userTask", "exclusiveGateway",
    // "startEvent", …). Only "obstacle-shaped" types are recorded —
    // Participant and Lane are deliberately excluded so the router treats
    // them as containers (sequence flows are *meant* to cross lane borders).
    private static readonly HashSet<string> ObstacleTypes = new(StringComparer.Ordinal)
    {
        "task", "userTask", "serviceTask", "manualTask", "sendTask",
        "receiveTask", "scriptTask", "businessRuleTask", "callActivity",
        "subProcess", "transaction", "adHocSubProcess",
        "startEvent", "endEvent",
        "intermediateThrowEvent", "intermediateCatchEvent", "boundaryEvent",
        "exclusiveGateway", "parallelGateway", "inclusiveGateway",
        "eventBasedGateway", "complexGateway",
        "dataObjectReference", "dataStoreReference"
    };
    internal static Dictionary<string, string> CollectShapeTypes(string xml)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(xml, @"<bpmn:(\w+)\s+id=""([^""]+)"""))
        {
            var type = m.Groups[1].Value;
            if (ObstacleTypes.Contains(type))
                map[m.Groups[2].Value] = type;
        }
        return map;
    }

    /// <summary>
    /// Pairwise overlap relaxation: walks every pair of "obstacle" shapes
    /// (tasks, events, gateways) and shifts the later one out of the way
    /// when its bounding box intersects the earlier one's. The shift axis
    /// is whichever direction has less overlap (so a tiny vertical
    /// intrusion is fixed by a vertical push, not a wholesale horizontal
    /// shove). Pool/lane containment is ignored — those are containers,
    /// not collisions.
    ///
    /// Reading direction matters: in LTR mode the "later" shape is
    /// rightmost and gets pushed further right; in RTL it's leftmost and
    /// gets pushed further left. That keeps the flow visually monotonic
    /// for the script of the labels.
    ///
    /// Iterates up to <c>MaxOverlapIterations</c> rounds — overlap fixes
    /// in one pass can create new overlaps with previously-clean
    /// neighbours, so a few rounds settle the layout.
    /// </summary>
    internal static string ResolveShapeOverlaps(
        string xml,
        Dictionary<string, ShapeBox> shapes,
        Dictionary<string, string> shapeTypes,
        bool isRtl)
    {
        const int MaxOverlapIterations = 8;
        const double MinGap = 20;

        // Only resolve among "obstacle" types. Pools and lanes are
        // containers (they're expected to wrap other shapes), so an
        // overlap between a Pool and a Task is not a collision.
        var ids = shapes.Where(kv => shapeTypes.ContainsKey(kv.Key))
                        .Select(kv => kv.Key)
                        .ToList();
        if (ids.Count < 2) return xml;

        var working = new Dictionary<string, ShapeBox>(shapes);
        bool anyChange = false;

        for (int iter = 0; iter < MaxOverlapIterations; iter++)
        {
            bool movedThisIter = false;
            for (int i = 0; i < ids.Count; i++)
            {
                for (int j = i + 1; j < ids.Count; j++)
                {
                    var a = working[ids[i]];
                    var b = working[ids[j]];
                    if (!BoxesOverlap(a, b)) continue;

                    double xOverlap = Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X);
                    double yOverlap = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);

                    if (xOverlap <= yOverlap)
                    {
                        // Horizontal push — direction follows reading order:
                        //   LTR: push the rightmost shape further right
                        //   RTL: push the leftmost shape further left
                        double shift = xOverlap + MinGap;
                        if (isRtl)
                        {
                            // Leftmost moves further left.
                            if (a.X <= b.X) working[ids[i]] = a with { X = a.X - shift };
                            else            working[ids[j]] = b with { X = b.X - shift };
                        }
                        else
                        {
                            // Rightmost moves further right.
                            if (a.X >= b.X) working[ids[i]] = a with { X = a.X + shift };
                            else            working[ids[j]] = b with { X = b.X + shift };
                        }
                    }
                    else
                    {
                        // Vertical push — always the lower shape goes down
                        // (orientation has no language equivalent here).
                        double shift = yOverlap + MinGap;
                        if (a.Y >= b.Y) working[ids[i]] = a with { Y = a.Y + shift };
                        else            working[ids[j]] = b with { Y = b.Y + shift };
                    }
                    movedThisIter = true;
                    anyChange = true;
                }
            }
            if (!movedThisIter) break;
        }

        if (!anyChange) return xml;

        // Push the new positions into the XML by rewriting every shape's
        // dc:Bounds. Width/height are preserved verbatim; only x/y change.
        var updated = Regex.Replace(xml,
            @"(<bpmndi:BPMNShape[^>]*bpmnElement="")([^""]+)(""[^>]*>\s*<dc:Bounds\s+x="")(-?\d+(?:\.\d+)?)(""\s+y="")(-?\d+(?:\.\d+)?)(""\s+width="")(\d+(?:\.\d+)?)(""\s+height="")(\d+(?:\.\d+)?)(""\s*/>)",
            match =>
            {
                var bpmnId = match.Groups[2].Value;
                if (!working.TryGetValue(bpmnId, out var n)) return match.Value;
                if (!shapes.TryGetValue(bpmnId, out var o)) return match.Value;
                if (Math.Abs(n.X - o.X) < 0.5 && Math.Abs(n.Y - o.Y) < 0.5) return match.Value;

                var inv = System.Globalization.CultureInfo.InvariantCulture;
                return match.Groups[1].Value + bpmnId + match.Groups[3].Value
                    + n.X.ToString("0.##", inv) + match.Groups[5].Value
                    + n.Y.ToString("0.##", inv) + match.Groups[7].Value
                    + match.Groups[8].Value + match.Groups[9].Value
                    + match.Groups[10].Value + match.Groups[11].Value;
            },
            RegexOptions.Singleline);

        // Keep the caller's bounds map in sync — used by the routing pass
        // that runs immediately after this. The container map shape (Pool/
        // Lane bounds) is left untouched, so flows still resolve against
        // the same containers.
        foreach (var id in ids) shapes[id] = working[id];
        return updated;
    }

    /// <summary>
    /// Axis-aligned bounding-box intersection. Returns true only on a
    /// strict overlap (touching edges are NOT a collision — adjacent
    /// shapes are a legitimate layout).
    /// </summary>
    private static bool BoxesOverlap(ShapeBox a, ShapeBox b)
    {
        return a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
    }

    /// <summary>
    /// Grow task shapes when the label clearly won't fit. Uses a coarse
    /// 7px/char × 1.1 line-height estimate — enough to catch the common
    /// "long Arabic title spills out of an 80-wide box" case without
    /// pretending to do real text metrics on the server.
    /// </summary>
    internal static string ResizeTaskShapesToFitLabels(
        string xml,
        Dictionary<string, ShapeBox> shapes,
        Dictionary<string, string> names)
    {
        const double charWidthPx = 7.0;     // ~Arial 12px lower bound
        const double sidePaddingPx = 16.0;
        const double minTaskWidth = 100.0;
        const double minTaskHeight = 80.0;

        return Regex.Replace(xml,
            @"(<bpmndi:BPMNShape[^>]*bpmnElement=""([^""]+)""[^>]*>\s*<dc:Bounds\s+x="")(-?\d+(?:\.\d+)?)(""\s+y="")(-?\d+(?:\.\d+)?)(""\s+width="")(\d+(?:\.\d+)?)(""\s+height="")(\d+(?:\.\d+)?)(""\s*/>)",
            match =>
            {
                var bpmnId = match.Groups[2].Value;
                if (!names.TryGetValue(bpmnId, out var label) || string.IsNullOrWhiteSpace(label))
                    return match.Value;
                if (!shapes.TryGetValue(bpmnId, out var box))
                    return match.Value;

                // Only resize bona-fide tasks. Pools, lanes, events and
                // gateways have fixed visual sizes; growing them would
                // wreck the layout the rest of this pass relies on.
                bool isTask = Regex.IsMatch(xml,
                    $@"<bpmn:(userTask|serviceTask|task|manualTask|sendTask|receiveTask|scriptTask|businessRuleTask|subProcess)\s+id=""{Regex.Escape(bpmnId)}""");
                if (!isTask) return match.Value;

                var needed = label.Length * charWidthPx + 2 * sidePaddingPx;
                var newWidth = Math.Max(Math.Max(box.W, minTaskWidth), needed);
                var newHeight = Math.Max(box.H, minTaskHeight);

                if (Math.Abs(newWidth - box.W) < 1 && Math.Abs(newHeight - box.H) < 1)
                    return match.Value;

                return match.Groups[1].Value
                    + match.Groups[3].Value
                    + match.Groups[4].Value
                    + match.Groups[5].Value
                    + match.Groups[6].Value
                    + newWidth.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + match.Groups[8].Value
                    + newHeight.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + match.Groups[10].Value;
            },
            RegexOptions.Singleline);
    }

    /// <summary>
    /// Rewrite every sequenceFlow / messageFlow BPMNEdge with an orthogonal
    /// waypoint chain that (a) touches shape boundaries instead of plunging
    /// through their centres and (b) routes around other tasks/events that
    /// happen to sit between source and target. Skips edges whose source or
    /// target is missing from <paramref name="shapes"/>.
    /// </summary>
    internal static string ReRouteSequenceFlowsOrthogonally(
        string xml,
        Dictionary<string, ShapeBox> shapes,
        Dictionary<string, string> shapeTypes)
    {
        // Source/target refs live on the corresponding <bpmn:sequenceFlow>
        // element, not the BPMNEdge — match the two in advance so the
        // replace loop below can be a single pass over BPMNEdges.
        var flowEnds = new Dictionary<string, (string src, string tgt)>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(xml,
            @"<bpmn:(?:sequenceFlow|messageFlow)\s+[^>]*id=""([^""]+)""[^>]*sourceRef=""([^""]+)""[^>]*targetRef=""([^""]+)"""))
        {
            flowEnds[m.Groups[1].Value] = (m.Groups[2].Value, m.Groups[3].Value);
        }

        return Regex.Replace(xml,
            @"(<bpmndi:BPMNEdge[^>]*bpmnElement="")([^""]+)(""[^>]*>)(.*?)(</bpmndi:BPMNEdge>)",
            edge =>
            {
                var open = edge.Groups[1].Value + edge.Groups[2].Value + edge.Groups[3].Value;
                var flowId = edge.Groups[2].Value;
                var body = edge.Groups[4].Value;
                var close = edge.Groups[5].Value;

                if (!flowEnds.TryGetValue(flowId, out var ends)) return edge.Value;
                if (!shapes.TryGetValue(ends.src, out var s) || !shapes.TryGetValue(ends.tgt, out var t))
                    return edge.Value;

                // Build the obstacle list for this edge: every concrete
                // shape that isn't the source or target. Pools and lanes
                // are already excluded by shapeTypes (containers, not
                // obstacles).
                var obstacles = new List<ShapeBox>();
                foreach (var kv in shapes)
                {
                    if (kv.Key == ends.src || kv.Key == ends.tgt) continue;
                    if (!shapeTypes.ContainsKey(kv.Key)) continue;
                    obstacles.Add(kv.Value);
                }

                var path = RouteAroundObstacles(s, t, obstacles);

                // Preserve any embedded BPMNLabel — CenterEdgeLabels handles
                // its position in a later pass, this just keeps the element
                // in place so the label doesn't disappear when we rewrite
                // the waypoint block.
                var labelMatch = Regex.Match(body, @"<bpmndi:BPMNLabel\b[^>]*>.*?</bpmndi:BPMNLabel>", RegexOptions.Singleline);
                var labelXml = labelMatch.Success ? "\n        " + labelMatch.Value : "";

                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var newWaypoints = new System.Text.StringBuilder();
                foreach (var (x, y) in path)
                {
                    newWaypoints.Append("\n        ");
                    newWaypoints.Append($@"<di:waypoint x=""{x.ToString("0.##", inv)}"" y=""{y.ToString("0.##", inv)}"" />");
                }
                newWaypoints.Append(labelXml);
                newWaypoints.Append("\n      ");

                return open + newWaypoints + close;
            },
            RegexOptions.Singleline);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Obstacle-aware routing
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generate a small set of candidate orthogonal routes between two
    /// shapes, score each by the number of obstacle crossings (then by
    /// total Manhattan length as a tiebreaker), and return the best.
    /// Candidates:
    ///   1. straight horizontal / vertical when source-target axes overlap
    ///   2. target-side L (out sideways, then in vertically)
    ///   3. source-side L (out vertically, then in sideways)
    ///   4. detour above all shapes (Z-shape over the top)
    ///   5. detour below all shapes (Z-shape under the bottom)
    /// The Z-detours are the fallback for crowded diagrams — they almost
    /// never cross another shape, so they reliably win when the direct L's
    /// are blocked.
    /// </summary>
    internal static IList<(double x, double y)> RouteAroundObstacles(
        ShapeBox s, ShapeBox t, IList<ShapeBox> obstacles)
    {
        var candidates = new List<List<(double x, double y)>>();

        bool vOverlap = s.Y < t.Bottom && t.Y < s.Bottom;
        bool hOverlap = s.X < t.Right && t.X < s.Right;

        // 1) Straight horizontal — only valid when shapes overlap vertically
        if (vOverlap)
        {
            double y = Math.Max(Math.Max(s.Y, t.Y) + 10, Math.Min(s.Cy, t.Cy));
            y = Math.Min(y, Math.Min(s.Bottom, t.Bottom) - 10);
            if (y < Math.Max(s.Y, t.Y) || y > Math.Min(s.Bottom, t.Bottom)) y = s.Cy;
            if (t.Cx >= s.Cx) candidates.Add(new() { (s.Right, y), (t.X, y) });
            else              candidates.Add(new() { (s.X, y),     (t.Right, y) });
        }

        // 1b) Straight vertical — only valid when shapes overlap horizontally
        if (hOverlap)
        {
            double x = Math.Max(Math.Max(s.X, t.X) + 10, Math.Min(s.Cx, t.Cx));
            x = Math.Min(x, Math.Min(s.Right, t.Right) - 10);
            if (x < Math.Max(s.X, t.X) || x > Math.Min(s.Right, t.Right)) x = s.Cx;
            if (t.Cy >= s.Cy) candidates.Add(new() { (x, s.Bottom), (x, t.Y) });
            else              candidates.Add(new() { (x, s.Y),      (x, t.Bottom) });
        }

        // 2) Target-side L (horizontal-then-vertical)
        // Exit on the source's facing horizontal side, bend over the
        // target's centre x, enter at the target's facing vertical side.
        {
            double sx = t.Cx >= s.Cx ? s.Right : s.X;
            double sy = s.Cy;
            double ex = t.Cx;
            double ey = t.Cy >= s.Cy ? t.Y : t.Bottom;
            candidates.Add(new() { (sx, sy), (ex, sy), (ex, ey) });
        }

        // 3) Source-side L (vertical-then-horizontal) — same destination
        // but bends at the source's vertical first.
        {
            double sx = s.Cx;
            double sy = t.Cy >= s.Cy ? s.Bottom : s.Y;
            double ex = t.Cx >= s.Cx ? t.X : t.Right;
            double ey = t.Cy;
            candidates.Add(new() { (sx, sy), (sx, ey), (ex, ey) });
        }

        // 4 & 5) Z-detours over/under all shapes. AllBoxes includes
        // source/target so the corridor clears every visible element by
        // the padding gap.
        const double detourPadding = 40;
        double minY = Math.Min(s.Y, t.Y);
        double maxY = Math.Max(s.Bottom, t.Bottom);
        foreach (var o in obstacles)
        {
            if (o.Y < minY) minY = o.Y;
            if (o.Bottom > maxY) maxY = o.Bottom;
        }

        // Above-detour
        {
            double corridorY = minY - detourPadding;
            candidates.Add(new()
            {
                (s.Cx, s.Y), (s.Cx, corridorY), (t.Cx, corridorY), (t.Cx, t.Y)
            });
        }
        // Below-detour
        {
            double corridorY = maxY + detourPadding;
            candidates.Add(new()
            {
                (s.Cx, s.Bottom), (s.Cx, corridorY), (t.Cx, corridorY), (t.Cx, t.Bottom)
            });
        }

        // Score every candidate and pick the winner. Tiebreak ordering:
        //   fewer obstacle crossings   →
        //   fewer bends (simpler path) →
        //   shorter total length.
        var best = candidates
            .Select(c => (
                path: (IList<(double x, double y)>)c,
                crossings: CountObstacleCrossings(c, obstacles),
                bends: c.Count - 2,
                length: ManhattanLength(c)))
            .OrderBy(x => x.crossings)
            .ThenBy(x => x.bends)
            .ThenBy(x => x.length)
            .First();

        return best.path;
    }

    /// <summary>
    /// Count axis-aligned segments of <paramref name="path"/> that cross
    /// the interior of any obstacle. The path is assumed to be orthogonal —
    /// non-orthogonal segments score 0 (they shouldn't exist in routes we
    /// generate).
    /// </summary>
    internal static int CountObstacleCrossings(
        IReadOnlyList<(double x, double y)> path,
        IList<ShapeBox> obstacles)
    {
        int n = 0;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            var p1 = path[i];
            var p2 = path[i + 1];
            foreach (var box in obstacles)
            {
                if (SegmentIntersectsBox(p1, p2, box)) n++;
            }
        }
        return n;
    }

    // Does an axis-aligned segment cross the interior of an axis-aligned
    // box? Touching the boundary is allowed — only a strictly-inside
    // crossing counts, otherwise the source/target shape would always
    // score as a crossing.
    private static bool SegmentIntersectsBox(
        (double x, double y) p1,
        (double x, double y) p2,
        ShapeBox b)
    {
        const double epsilon = 0.5;
        // Horizontal segment
        if (Math.Abs(p1.y - p2.y) < epsilon)
        {
            double y = p1.y;
            if (y <= b.Y || y >= b.Bottom) return false;
            double minX = Math.Min(p1.x, p2.x);
            double maxX = Math.Max(p1.x, p2.x);
            return maxX > b.X && minX < b.Right;
        }
        // Vertical segment
        if (Math.Abs(p1.x - p2.x) < epsilon)
        {
            double x = p1.x;
            if (x <= b.X || x >= b.Right) return false;
            double minY = Math.Min(p1.y, p2.y);
            double maxY = Math.Max(p1.y, p2.y);
            return maxY > b.Y && minY < b.Bottom;
        }
        return false;
    }

    private static double ManhattanLength(IList<(double x, double y)> path)
    {
        double total = 0;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            total += Math.Abs(path[i + 1].x - path[i].x) + Math.Abs(path[i + 1].y - path[i].y);
        }
        return total;
    }

    /// <summary>
    /// Re-centre every edge's BPMNLabel on the new midpoint we just
    /// computed for the edge. bpmn-js's default places labels at the
    /// midpoint too, so this matches what a fresh export would produce.
    /// </summary>
    internal static string CenterEdgeLabels(string xml, Dictionary<string, ShapeBox> shapes)
    {
        var flowEnds = new Dictionary<string, (string src, string tgt)>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(xml,
            @"<bpmn:(?:sequenceFlow|messageFlow)\s+[^>]*id=""([^""]+)""[^>]*sourceRef=""([^""]+)""[^>]*targetRef=""([^""]+)"""))
        {
            flowEnds[m.Groups[1].Value] = (m.Groups[2].Value, m.Groups[3].Value);
        }

        return Regex.Replace(xml,
            @"(<bpmndi:BPMNEdge[^>]*bpmnElement="")([^""]+)(""[^>]*>)(.*?)(</bpmndi:BPMNEdge>)",
            edge =>
            {
                var flowId = edge.Groups[2].Value;
                var body = edge.Groups[4].Value;
                if (!body.Contains("<bpmndi:BPMNLabel", StringComparison.Ordinal))
                    return edge.Value;
                if (!flowEnds.TryGetValue(flowId, out var ends)) return edge.Value;
                if (!shapes.TryGetValue(ends.src, out var s) || !shapes.TryGetValue(ends.tgt, out var t))
                    return edge.Value;

                var midX = (s.Cx + t.Cx) / 2;
                var midY = (s.Cy + t.Cy) / 2;
                var inv = System.Globalization.CultureInfo.InvariantCulture;

                // The label may or may not have its own <dc:Bounds>. Replace
                // whatever it has with a centred 90×27 box (bpmn-js default
                // label dimensions). Slightly offset above the midpoint so
                // the label doesn't sit on top of the edge.
                var newLabel = $"<bpmndi:BPMNLabel><dc:Bounds x=\"{(midX - 45).ToString("0.##", inv)}\" y=\"{(midY - 25).ToString("0.##", inv)}\" width=\"90\" height=\"27\" /></bpmndi:BPMNLabel>";
                var newBody = Regex.Replace(body,
                    @"<bpmndi:BPMNLabel\b[^>]*>.*?</bpmndi:BPMNLabel>",
                    newLabel,
                    RegexOptions.Singleline);

                return edge.Groups[1].Value + edge.Groups[2].Value + edge.Groups[3].Value
                       + newBody
                       + edge.Groups[5].Value;
            },
            RegexOptions.Singleline);
    }
}
