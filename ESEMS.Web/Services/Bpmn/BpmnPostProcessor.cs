using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ESEMS.Web.Services.Bpmn;

/// <summary>
/// Best-effort upgrades to BPMN XML that the AI usually gets wrong:
/// promotes generic <c>&lt;task&gt;</c> elements to <c>&lt;userTask&gt;</c>
/// or <c>&lt;serviceTask&gt;</c> when the name gives a high-confidence
/// signal. Conservative by design — if a name doesn't clearly match
/// either bucket, the element is left untouched (still renders as a
/// generic task).
///
/// Runs AFTER validation so malformed XML never reaches this step.
/// </summary>
public sealed class BpmnPostProcessor : IBpmnPostProcessor
{
    // High-confidence human-action patterns. English verbs cover
    // typical Dubai/UAE government workflow vocabulary; Arabic roots
    // use partial matches to tolerate conjugation/prefixes.
    private static readonly Regex UserActionPattern = new(
        @"\b(review|approve|decide|select|choose|sign|fill|enter|submit|verify|validate|assign|check|read|inspect|confirm|authoriz|accept|reject|return)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Arabic user-action roots (no word boundaries — Arabic has no
    // ASCII word separators in many cases).
    private static readonly string[] UserActionArabicRoots =
    {
        "مراجع", "اعتماد", "اعتمد", "توقيع", "وقع", "تعبئة", "عبّ", "إدخال", "ادخال",
        "تقديم", "قدّم", "تحقق", "تأكد", "اختر", "اختيار", "قرر", "قرار", "موافق", "رفض"
    };

    // Service-task patterns: automated/system actions.
    private static readonly Regex ServiceActionPattern = new(
        @"\b(send\s+email|email\s+notif|notify|generate\s+(report|document|pdf)|call\s+api|api\s+call|query\s+database|integrat|sync|fetch|update\s+system|auto\-?generate|system\s+log|export\s+to|publish\s+to)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] ServiceActionArabicRoots =
    {
        "إرسال", "أرسل", "إشعار", "توليد", "تصدير", "مزامن", "استعلام"
    };

    public BpmnPostProcessResult UpgradeTaskTypes(string bpmnXml)
    {
        if (string.IsNullOrWhiteSpace(bpmnXml))
            return new BpmnPostProcessResult(bpmnXml ?? string.Empty, 0, 0);

        XDocument doc;
        try { doc = XDocument.Parse(bpmnXml, LoadOptions.PreserveWhitespace); }
        catch { return new BpmnPostProcessResult(bpmnXml, 0, 0); }

        var bpmnNs = doc.Root?.GetNamespaceOfPrefix("bpmn")
                     ?? XNamespace.Get("http://www.omg.org/spec/BPMN/20100524/MODEL");

        int userTaskPromotions = 0;
        int serviceTaskPromotions = 0;

        var genericTasks = doc.Descendants().Where(e => e.Name.LocalName == "task").ToList();
        foreach (var task in genericTasks)
        {
            var name = task.Attribute("name")?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (MatchesAny(name, UserActionPattern, UserActionArabicRoots))
            {
                task.Name = bpmnNs + "userTask";
                userTaskPromotions++;
            }
            else if (MatchesAny(name, ServiceActionPattern, ServiceActionArabicRoots))
            {
                task.Name = bpmnNs + "serviceTask";
                serviceTaskPromotions++;
            }
        }

        return new BpmnPostProcessResult(doc.ToString(SaveOptions.DisableFormatting), userTaskPromotions, serviceTaskPromotions);
    }

    private static bool MatchesAny(string name, Regex englishPattern, string[] arabicRoots)
    {
        if (englishPattern.IsMatch(name)) return true;
        foreach (var root in arabicRoots)
            if (name.Contains(root, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>
    /// bpmn-moddle (and the XML NCName production in general) rejects IDs
    /// containing characters outside ASCII letters/digits/<c>._-</c>. LLMs
    /// routinely paste Arabic names into IDs (e.g.
    /// <c>Process_إدارة_التغيير</c>), which parses fine at the XML layer
    /// (the XML 1.0 Letter class does allow Arabic) but bpmn-js throws
    /// "illegal ID" and renders nothing. This pass rewrites every
    /// non-ASCII-safe ID to <c>{LocalName}_{n}</c> and updates every
    /// reference in the document — attributes whose value equals an old
    /// ID, plus text content of leaf elements like
    /// <c>&lt;incoming&gt;/&lt;outgoing&gt;</c>.
    /// </summary>
    public BpmnIdSanitizeResult SanitizeIds(string bpmnXml)
    {
        if (string.IsNullOrWhiteSpace(bpmnXml))
            return new BpmnIdSanitizeResult(bpmnXml ?? string.Empty, 0);

        XDocument doc;
        try { doc = XDocument.Parse(bpmnXml, LoadOptions.PreserveWhitespace); }
        catch { return new BpmnIdSanitizeResult(bpmnXml, 0); }

        var idMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        var typeCounters = new Dictionary<string, int>(StringComparer.Ordinal);

        // First pass: record every existing id so generated ones don't collide.
        foreach (var el in doc.Descendants())
        {
            var existing = el.Attribute("id")?.Value;
            if (!string.IsNullOrEmpty(existing) && IsAsciiNcName(existing))
                usedIds.Add(existing);
        }

        // Second pass: sanitize non-NCName ids.
        foreach (var el in doc.Descendants())
        {
            var idAttr = el.Attribute("id");
            if (idAttr == null) continue;

            var id = idAttr.Value;
            if (string.IsNullOrEmpty(id) || IsAsciiNcName(id)) continue;

            var local = el.Name.LocalName;
            typeCounters.TryGetValue(local, out var c);
            string newId;
            do
            {
                c++;
                newId = $"{local}_{c}";
            } while (usedIds.Contains(newId));
            typeCounters[local] = c;
            usedIds.Add(newId);
            idMap[id] = newId;
            idAttr.Value = newId;
        }

        if (idMap.Count == 0)
            return new BpmnIdSanitizeResult(bpmnXml, 0);

        // Third pass: rewrite every reference. Any attribute whose value
        // equals a renamed id gets updated (covers processRef, sourceRef,
        // targetRef, bpmnElement, attachedToRef, default, etc.). Leaf
        // elements with an exact-match text value are also rewritten
        // (covers <incoming>/<outgoing>/<dataInputRefs>/etc.).
        foreach (var el in doc.Descendants())
        {
            foreach (var attr in el.Attributes())
            {
                if (attr.Name.LocalName == "id") continue;
                if (idMap.TryGetValue(attr.Value, out var mapped))
                    attr.Value = mapped;
            }

            if (!el.HasElements)
            {
                var trimmed = el.Value.Trim();
                if (!string.IsNullOrEmpty(trimmed) && idMap.TryGetValue(trimmed, out var mapped))
                    el.Value = mapped;
            }
        }

        return new BpmnIdSanitizeResult(doc.ToString(SaveOptions.DisableFormatting), idMap.Count);
    }

    // XML NCName under the subset that bpmn-moddle actually accepts:
    // ASCII letter or underscore to start; ASCII alphanumerics plus _, -, .
    // thereafter. We deliberately do NOT allow the full XML 1.0 Letter
    // range (which includes Arabic) — bpmn-moddle rejects those.
    private static bool IsAsciiNcName(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        var c = s[0];
        if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_'))
            return false;
        for (int i = 1; i < s.Length; i++)
        {
            c = s[i];
            var ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                     || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';
            if (!ok) return false;
        }
        return true;
    }

    /// <summary>
    /// Auto-heals a class of LLM drift we see repeatedly: the model emits
    /// <c>&lt;sequenceFlow id="SequenceFlow_1"/&gt;</c> in the process, then
    /// <c>&lt;BPMNEdge bpmnElement="Flow_1"/&gt;</c> in the DI — different
    /// prefix, same suffix. The DI ref is an orphan as far as the validator
    /// is concerned. Where the orphan's trailing id part UNIQUELY matches a
    /// real id in the process, rewrite the ref to the real id.
    /// Conservative: only rewrites on a single unambiguous candidate.
    /// </summary>
    public BpmnDiRefFixResult FixDiRefs(string bpmnXml)
    {
        if (string.IsNullOrWhiteSpace(bpmnXml))
            return new BpmnDiRefFixResult(bpmnXml ?? string.Empty, 0, 0);

        XDocument doc;
        try { doc = XDocument.Parse(bpmnXml, LoadOptions.PreserveWhitespace); }
        catch { return new BpmnDiRefFixResult(bpmnXml, 0, 0); }

        // Collect every id declared outside the DI section.
        var processIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var el in doc.Descendants())
        {
            if (el.Name.LocalName is "BPMNShape" or "BPMNEdge" or "BPMNDiagram" or "BPMNPlane") continue;
            var id = el.Attribute("id")?.Value;
            if (!string.IsNullOrEmpty(id)) processIds.Add(id);
        }

        // Build suffix → [ids] index for quick lookup. Suffix = token after
        // the last underscore (e.g., "Flow_1" → "1", "Task_SubmitRequest"
        // → "SubmitRequest", "StartEvent_Begin" → "Begin").
        var bySuffix = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var id in processIds)
        {
            var suffix = SuffixOf(id);
            if (string.IsNullOrEmpty(suffix)) continue;
            if (!bySuffix.TryGetValue(suffix, out var bucket))
                bySuffix[suffix] = bucket = new List<string>();
            bucket.Add(id);
        }

        int rewrites = 0;
        int stillOrphan = 0;

        foreach (var diEl in doc.Descendants().Where(e => e.Name.LocalName is "BPMNShape" or "BPMNEdge"))
        {
            var attr = diEl.Attribute("bpmnElement");
            if (attr == null) continue;
            var refId = attr.Value;
            if (string.IsNullOrEmpty(refId) || processIds.Contains(refId)) continue;

            var suffix = SuffixOf(refId);
            if (string.IsNullOrEmpty(suffix)) { stillOrphan++; continue; }

            if (bySuffix.TryGetValue(suffix, out var candidates) && candidates.Count == 1)
            {
                attr.Value = candidates[0];
                rewrites++;
            }
            else
            {
                stillOrphan++;
            }
        }

        var output = rewrites > 0 ? doc.ToString(SaveOptions.DisableFormatting) : bpmnXml;
        return new BpmnDiRefFixResult(output, rewrites, stillOrphan);
    }

    private static string SuffixOf(string id)
    {
        var i = id.LastIndexOf('_');
        return i >= 0 && i < id.Length - 1 ? id[(i + 1)..] : id;
    }
}

public interface IBpmnPostProcessor
{
    BpmnPostProcessResult UpgradeTaskTypes(string bpmnXml);
    BpmnDiRefFixResult FixDiRefs(string bpmnXml);
    BpmnIdSanitizeResult SanitizeIds(string bpmnXml);
}

public sealed record BpmnPostProcessResult(string BpmnXml, int UserTaskPromotions, int ServiceTaskPromotions)
{
    public int TotalPromotions => UserTaskPromotions + ServiceTaskPromotions;
}

public sealed record BpmnDiRefFixResult(string BpmnXml, int Rewrites, int StillOrphan);

public sealed record BpmnIdSanitizeResult(string BpmnXml, int IdsRewritten);
