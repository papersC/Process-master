using System.Text.RegularExpressions;

namespace ESEMS.Web.Services.Bpmn;

/// <summary>
/// Local, deterministic extraction of business-process content from Visio
/// <c>page*.xml</c>. Produces a plain-text description plus an ordered step
/// list that the AI Diagrams pipeline (prompt-improvement →
/// GenerateBPMNDiagramAsync) can consume — same input shape as a user
/// typing the process into the /AI/Diagrams form.
///
/// No LLM calls. Pure XML parsing via regex, resilient to missing fields.
/// </summary>
public sealed class VisioExtractor : IVisioExtractor
{
    public VisioExtractResult Extract(string visioXml, string sheetName)
    {
        if (string.IsNullOrWhiteSpace(visioXml))
            return new VisioExtractResult(sheetName, string.Empty, Array.Empty<string>());

        // Shape id → stripped text.
        var shapes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(visioXml,
            @"<Shape\s+ID=""(?<id>\d+)""[^>]*>(?<body>[\s\S]*?)</Shape>",
            RegexOptions.Compiled))
        {
            var id = m.Groups["id"].Value;
            var body = m.Groups["body"].Value;
            // Pull the first <Text> block inside the shape. Visio nests more
            // <Shape> inside a group; we want the text directly attached to
            // THIS shape, so look only at the opening <Text> that precedes
            // any child <Shape>.
            var textMatch = Regex.Match(body, @"<Text>([\s\S]*?)</Text>");
            if (!textMatch.Success) continue;
            var text = StripVisioFormatting(textMatch.Groups[1].Value).Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            shapes[id] = text;
        }

        // Connection pairs (FromSheet → ToSheet).
        var edges = new List<(string From, string To)>();
        foreach (Match m in Regex.Matches(visioXml,
            @"<Connect\s+[^>]*FromSheet=""(?<from>\d+)""[^>]*ToSheet=""(?<to>\d+)""",
            RegexOptions.Compiled))
        {
            edges.Add((m.Groups["from"].Value, m.Groups["to"].Value));
        }

        // Some Visio diagrams use a connector SHAPE as intermediary:
        // connector A → connector-shape → connector B. Collapse those so the
        // flow reads A → B even when the connector shape has no text.
        var shapeTextKnown = new HashSet<string>(shapes.Keys, StringComparer.Ordinal);
        var resolvedEdges = new List<(string From, string To)>();
        foreach (var (from, to) in edges)
        {
            var fromTextShape = shapeTextKnown.Contains(from) ? from : ResolveText(from, edges, shapeTextKnown);
            var toTextShape = shapeTextKnown.Contains(to) ? to : ResolveText(to, edges, shapeTextKnown);
            if (fromTextShape is not null && toTextShape is not null && fromTextShape != toTextShape)
                resolvedEdges.Add((fromTextShape, toTextShape));
        }

        // Steps list = all shape texts, deduplicated, preserving declaration order.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var steps = new List<string>();
        foreach (var kv in shapes)
        {
            if (seen.Add(kv.Value))
                steps.Add(kv.Value);
        }

        // Description — machine-friendly prose the prompt-improver can expand.
        var sb = new System.Text.StringBuilder();
        sb.Append("Business process: ").Append(sheetName).Append(". ");

        if (steps.Count > 0)
        {
            sb.Append("Activities and roles extracted from the source diagram: ");
            sb.Append(string.Join("؛ ", steps)).Append(". ");
        }

        if (resolvedEdges.Count > 0)
        {
            var flowParts = resolvedEdges
                .Where(e => shapes.ContainsKey(e.From) && shapes.ContainsKey(e.To))
                .Select(e => $"{shapes[e.From]} → {shapes[e.To]}")
                .Distinct()
                .Take(40)  // cap — avoid prompt bloat on very connected diagrams
                .ToList();
            if (flowParts.Count > 0)
            {
                sb.Append("Connections in the source diagram (read these as sequence or message flows depending on whether they cross swimlanes): ");
                sb.Append(string.Join(" | ", flowParts)).Append(".");
            }
        }

        return new VisioExtractResult(sheetName, sb.ToString().Trim(), steps);
    }

    private static string? ResolveText(string shapeId, List<(string From, string To)> edges, HashSet<string> textShapes)
    {
        // Walk forward at most 2 hops to pass through a connector shape.
        foreach (var e in edges)
        {
            if (e.From != shapeId) continue;
            if (textShapes.Contains(e.To)) return e.To;
        }
        return null;
    }

    // Visio text blocks embed <cp IX='N'/>, <pp IX='N'/>, <tp IX='N'/>
    // formatting markers, plus entity refs. Strip them to get the readable
    // label that belongs on the BPMN element.
    private static string StripVisioFormatting(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var s = Regex.Replace(raw, @"<(cp|pp|tp|fld)\s*[^/]*/?>", string.Empty);
        s = Regex.Replace(s, @"<[^>]+>", string.Empty);
        s = s.Replace("&#xA;", " ").Replace("\n", " ").Replace("\r", " ");
        s = s.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
        return Regex.Replace(s, @"\s+", " ").Trim();
    }
}

public interface IVisioExtractor
{
    VisioExtractResult Extract(string visioXml, string sheetName);
}

public sealed record VisioExtractResult(string Title, string Description, IReadOnlyList<string> Steps);
