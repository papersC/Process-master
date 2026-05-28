using System.Text.RegularExpressions;
using ESEMS.Web.Data;
using ESEMS.Web.Models.APQC;
using Microsoft.EntityFrameworkCore;

namespace ESEMS.Web.Services.Bpmn;

/// <summary>
/// Service interface for the on-disk BPMN library importer. Reads every
/// .bpmn file from a folder (default: <c>output2/</c> at the repo root),
/// parses the Arabic name out of each filename, fuzzy-matches it against
/// L3 Processes by NameAr/NameEn, and produces a preview the user can
/// confirm before anything is written to the DB.
/// </summary>
public interface IBpmnLibraryImporter
{
    /// <summary>
    /// Read the BPMN folder and return a per-file mapping suggestion.
    /// No DB writes happen — the caller shows this to the user, who then
    /// posts the chosen IDs back to <see cref="ImportAsync"/>.
    /// </summary>
    Task<BpmnLibraryPreview> PreviewAsync(string folderPath, CancellationToken ct = default);

    /// <summary>
    /// Apply the user-confirmed mapping. For each file:
    ///   - matched: set Process.BpmnDiagram + create a new ProcessBpmnVersion row
    ///   - unmatched: store the XML as an OrphanBpmnDrawing for later linkage
    /// Idempotent: re-importing the same file onto the same Process bumps
    /// the version number instead of duplicating.
    /// </summary>
    Task<BpmnLibraryImportResult> ImportAsync(
        string folderPath,
        IReadOnlyDictionary<string, string?> confirmedMappings,
        string? actingUserName,
        CancellationToken ct = default);
}

/// <summary>Per-file mapping suggestion returned by <see cref="IBpmnLibraryImporter.PreviewAsync"/>.</summary>
public sealed class BpmnLibraryFileMapping
{
    public string FileName { get; set; } = string.Empty;
    public string? FilePrefix { get; set; }
    public string DetectedName { get; set; } = string.Empty;
    public int XmlSizeBytes { get; set; }

    /// <summary>Suggested Process.Id (null when nothing matched above the threshold).</summary>
    public string? SuggestedProcessId { get; set; }
    public string? SuggestedProcessCode { get; set; }
    public string? SuggestedProcessNameAr { get; set; }
    public string? SuggestedProcessNameEn { get; set; }

    /// <summary>0..1 similarity score for the suggested match. Null if no match.</summary>
    public double? Score { get; set; }

    /// <summary>How the match was decided: <c>code_exact</c>, <c>name_substring</c>, <c>fuzzy_X.XX</c>, <c>none</c>.</summary>
    public string MatchHow { get; set; } = "none";

    /// <summary>True when the Process already has a BpmnDiagram — confirming this overwrites it (the old one is preserved as a ProcessBpmnVersion).</summary>
    public bool ProcessAlreadyHasDiagram { get; set; }

    /// <summary>Pre-populated alternate matches (top-5 fuzzy candidates) so the UI can offer a dropdown.</summary>
    public List<BpmnLibraryAlternative> Alternatives { get; set; } = new();

    /// <summary>True when the file is unreadable / not valid BPMN. The XML is still surfaced but the row is gated off.</summary>
    public bool ReadError { get; set; }
    public string? ReadErrorMessage { get; set; }
}

public sealed class BpmnLibraryAlternative
{
    public string ProcessId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? NameEn { get; set; }
    public double Score { get; set; }
}

public sealed class BpmnLibraryPreview
{
    public string FolderPath { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public int MatchedCount { get; set; }
    public int UnmatchedCount { get; set; }
    public int ReadErrorCount { get; set; }
    public List<BpmnLibraryFileMapping> Files { get; set; } = new();
}

public sealed class BpmnLibraryImportResult
{
    public int FilesProcessed { get; set; }
    public int Linked { get; set; }
    public int Orphaned { get; set; }
    public int Skipped { get; set; }
    public int VersionsCreated { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class BpmnLibraryImporter : IBpmnLibraryImporter
{
    // Fuzzy threshold below which we don't suggest a match. Picked to mirror
    // the ImportController.FindBestMatch helper used elsewhere — 0.70 keeps
    // strong Arabic-name matches and rejects "two-word in common, rest different"
    // false positives.
    private const double FuzzyAcceptThreshold = 0.70;

    // How many fuzzy candidates to surface per file for the dropdown.
    private const int AlternativesPerFile = 5;

    // Filename pattern: leading digits + underscore + the actual name.
    private static readonly Regex FilenameSplit = new(@"^(?<prefix>\d+)[_\s]+(?<name>.+)$", RegexOptions.Compiled);

    private readonly ApplicationDbContext _db;
    private readonly IBpmnProcessingService _bpmnProcessing;
    private readonly ILogger<BpmnLibraryImporter> _logger;

    public BpmnLibraryImporter(
        ApplicationDbContext db,
        IBpmnProcessingService bpmnProcessing,
        ILogger<BpmnLibraryImporter> logger)
    {
        _db = db;
        _bpmnProcessing = bpmnProcessing;
        _logger = logger;
    }

    public async Task<BpmnLibraryPreview> PreviewAsync(string folderPath, CancellationToken ct = default)
    {
        var preview = new BpmnLibraryPreview { FolderPath = folderPath };
        if (!Directory.Exists(folderPath))
        {
            _logger.LogWarning("BPMN library folder not found: {Folder}", folderPath);
            return preview;
        }

        var files = Directory.GetFiles(folderPath, "*.bpmn", SearchOption.TopDirectoryOnly)
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                             .ToArray();
        preview.TotalFiles = files.Length;
        if (files.Length == 0) return preview;

        // Pre-load the L3 catalog once. NameAr/NameEn/Code are all we need for matching.
        // Linq-to-DB query is materialized so the fuzzy loop stays in memory.
        var candidates = await _db.Processes
            .Where(p => !p.IsDeleted)
            .Select(p => new ProcessCandidate
            {
                Id = p.Id,
                Code = p.Code,
                NameAr = p.NameAr,
                NameEn = p.NameEn,
                HasDiagram = !string.IsNullOrEmpty(p.BpmnDiagram)
            })
            .ToListAsync(ct);

        // Pre-compute normalized forms once per candidate.
        foreach (var c in candidates)
        {
            c.NormNameAr = NormalizeForMatch(c.NameAr);
            c.NormNameEn = NormalizeForMatch(c.NameEn);
        }

        foreach (var path in files)
        {
            ct.ThrowIfCancellationRequested();
            var mapping = new BpmnLibraryFileMapping
            {
                FileName = Path.GetFileName(path)
            };

            // Parse "NNN_<name>.bpmn".
            var stem = Path.GetFileNameWithoutExtension(mapping.FileName);
            var m = FilenameSplit.Match(stem);
            if (m.Success)
            {
                mapping.FilePrefix = m.Groups["prefix"].Value;
                mapping.DetectedName = m.Groups["name"].Value.Trim();
            }
            else
            {
                mapping.DetectedName = stem.Trim();
            }

            // Read content but tolerate failures — we still want to show the file.
            string xml = string.Empty;
            try
            {
                xml = await File.ReadAllTextAsync(path, ct);
                mapping.XmlSizeBytes = System.Text.Encoding.UTF8.GetByteCount(xml);
                if (!_bpmnProcessing.LooksLikeBpmnXml(xml))
                {
                    mapping.ReadError = true;
                    mapping.ReadErrorMessage = "File does not look like a BPMN 2.0 document";
                }
            }
            catch (Exception ex)
            {
                mapping.ReadError = true;
                mapping.ReadErrorMessage = ex.Message;
                _logger.LogWarning(ex, "Could not read BPMN file {File}", path);
            }

            // Fuzzy-match the detected name against the L3 catalog.
            var (best, alternatives) = RankCandidates(mapping.DetectedName, candidates);

            if (best != null)
            {
                mapping.SuggestedProcessId = best.Candidate.Id;
                mapping.SuggestedProcessCode = best.Candidate.Code;
                mapping.SuggestedProcessNameAr = best.Candidate.NameAr;
                mapping.SuggestedProcessNameEn = best.Candidate.NameEn;
                mapping.Score = best.Score;
                mapping.MatchHow = best.How;
                mapping.ProcessAlreadyHasDiagram = best.Candidate.HasDiagram;
            }

            mapping.Alternatives = alternatives
                .Take(AlternativesPerFile)
                .Select(a => new BpmnLibraryAlternative
                {
                    ProcessId = a.Candidate.Id,
                    Code = a.Candidate.Code,
                    NameAr = a.Candidate.NameAr,
                    NameEn = a.Candidate.NameEn,
                    Score = a.Score
                })
                .ToList();

            preview.Files.Add(mapping);
        }

        preview.MatchedCount = preview.Files.Count(f => f.SuggestedProcessId != null);
        preview.UnmatchedCount = preview.Files.Count(f => f.SuggestedProcessId == null && !f.ReadError);
        preview.ReadErrorCount = preview.Files.Count(f => f.ReadError);

        return preview;
    }

    public async Task<BpmnLibraryImportResult> ImportAsync(
        string folderPath,
        IReadOnlyDictionary<string, string?> confirmedMappings,
        string? actingUserName,
        CancellationToken ct = default)
    {
        var result = new BpmnLibraryImportResult();
        if (!Directory.Exists(folderPath))
        {
            result.Errors.Add($"Folder not found: {folderPath}");
            return result;
        }

        var files = Directory.GetFiles(folderPath, "*.bpmn", SearchOption.TopDirectoryOnly)
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                             .ToArray();

        // Snapshot of orphan filenames so re-import doesn't create a duplicate orphan row.
        var existingOrphanNames = await _db.OrphanBpmnDrawings
            .Where(o => o.LinkedProcessId == null)
            .Select(o => o.FileName)
            .ToListAsync(ct);
        var orphanSet = new HashSet<string>(existingOrphanNames, StringComparer.OrdinalIgnoreCase);

        foreach (var path in files)
        {
            ct.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(path);
            result.FilesProcessed++;

            string xml;
            try { xml = await File.ReadAllTextAsync(path, ct); }
            catch (Exception ex)
            {
                result.Errors.Add($"{fileName}: read error — {ex.Message}");
                result.Skipped++;
                continue;
            }
            if (!_bpmnProcessing.LooksLikeBpmnXml(xml))
            {
                result.Errors.Add($"{fileName}: not a valid BPMN document");
                result.Skipped++;
                continue;
            }

            // What did the user choose for this file?
            // - missing key OR explicit empty string → orphan
            // - non-empty value → Process.Id to link
            confirmedMappings.TryGetValue(fileName, out var chosenProcessId);

            var stem = Path.GetFileNameWithoutExtension(fileName);
            var nameMatch = FilenameSplit.Match(stem);
            var detectedName = nameMatch.Success ? nameMatch.Groups["name"].Value.Trim() : stem.Trim();
            var prefix = nameMatch.Success ? nameMatch.Groups["prefix"].Value : null;

            if (!string.IsNullOrWhiteSpace(chosenProcessId))
            {
                var process = await _db.Processes.FirstOrDefaultAsync(p => p.Id == chosenProcessId, ct);
                if (process == null)
                {
                    result.Errors.Add($"{fileName}: chosen process '{chosenProcessId}' not found");
                    result.Skipped++;
                    continue;
                }

                // Archive the prior diagram (if any) as a version, then overwrite.
                if (!string.IsNullOrWhiteSpace(process.BpmnDiagram) && process.BpmnDiagram != xml)
                {
                    await ArchiveExistingDiagramAsync(process, $"Replaced by library import: {fileName}", actingUserName, ct);
                }

                process.BpmnDiagram = xml;
                process.BpmnFilePath = path;
                process.UpdatedAt = DateTime.UtcNow;

                // New version row stamped with this import.
                var nextVersion = await NextVersionNumberAsync(process.Id, ct);
                _db.ProcessBpmnVersions.Add(new ProcessBpmnVersion
                {
                    Id = Guid.NewGuid().ToString(),
                    ProcessId = process.Id,
                    VersionNumber = nextVersion,
                    BpmnXml = xml,
                    ChangeDescription = $"Imported from library file '{fileName}'",
                    CreatedByName = actingUserName ?? "System/Library-Import",
                    CreatedAt = DateTime.UtcNow,
                    IsCurrent = true,
                    XmlSizeBytes = System.Text.Encoding.UTF8.GetByteCount(xml)
                });
                result.VersionsCreated++;
                result.Linked++;

                // If this file was a pending orphan from a previous run, retire it.
                var existingOrphan = await _db.OrphanBpmnDrawings
                    .FirstOrDefaultAsync(o => o.FileName == fileName && o.LinkedProcessId == null, ct);
                if (existingOrphan != null)
                {
                    existingOrphan.LinkedProcessId = process.Id;
                    existingOrphan.LinkedAt = DateTime.UtcNow;
                    existingOrphan.LinkedByName = actingUserName;
                    existingOrphan.UpdatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                // Orphan path: store the XML for later linkage. De-dup by filename
                // so re-running the importer doesn't pile up identical orphans.
                if (orphanSet.Contains(fileName))
                {
                    result.Skipped++;
                    continue;
                }

                _db.OrphanBpmnDrawings.Add(new OrphanBpmnDrawing
                {
                    FileName = fileName,
                    DetectedName = detectedName,
                    FilePrefix = prefix,
                    BpmnXml = xml,
                    XmlSizeBytes = System.Text.Encoding.UTF8.GetByteCount(xml),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                orphanSet.Add(fileName);
                result.Orphaned++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Matching helpers
    // ─────────────────────────────────────────────────────────────────────

    private sealed class ProcessCandidate
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string? NameEn { get; set; }
        public bool HasDiagram { get; set; }
        public string NormNameAr { get; set; } = string.Empty;
        public string NormNameEn { get; set; } = string.Empty;
    }

    private sealed record RankedMatch(ProcessCandidate Candidate, double Score, string How);

    /// <summary>
    /// Three-tier matcher (mirrors ImportController.FindBestMatch):
    ///   1. code exact (case-insensitive)
    ///   2. normalized substring either way
    ///   3. Levenshtein ratio on NameAr or NameEn
    /// Returns the best match (or null) plus the top-N ranked alternatives.
    /// </summary>
    private static (RankedMatch? best, List<RankedMatch> alternatives) RankCandidates(
        string rawKey,
        IReadOnlyList<ProcessCandidate> candidates)
    {
        var trimmed = rawKey.Trim();
        var normKey = NormalizeForMatch(trimmed);

        // Tier 1 — exact code.
        var codeHit = candidates.FirstOrDefault(c =>
            !string.IsNullOrWhiteSpace(c.Code)
            && c.Code.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (codeHit != null)
            return (new RankedMatch(codeHit, 1.0, "code_exact"), new List<RankedMatch>());

        // Score every candidate so we can pick #1 and surface alternatives.
        var ranked = new List<RankedMatch>(candidates.Count);
        foreach (var c in candidates)
        {
            // Substring win: treat as 0.95 so it ranks above any fuzzy-only match.
            if (IsNormalizedSubstringMatch(c.NormNameAr, normKey)
                || IsNormalizedSubstringMatch(c.NormNameEn, normKey))
            {
                ranked.Add(new RankedMatch(c, 0.95, "name_substring"));
                continue;
            }

            var arScore = LevenshteinRatio(c.NormNameAr, normKey);
            var enScore = LevenshteinRatio(c.NormNameEn, normKey);
            var score = Math.Max(arScore, enScore);
            ranked.Add(new RankedMatch(c, score, $"fuzzy_{score:F2}"));
        }

        var ordered = ranked.OrderByDescending(r => r.Score).ToList();
        var best = ordered.FirstOrDefault();
        if (best == null || best.Score < FuzzyAcceptThreshold)
        {
            // Still return ordered alternatives — the operator might want to
            // pick a sub-threshold candidate manually from the dropdown.
            return (null, ordered);
        }

        return (best, ordered.Skip(1).ToList());
    }

    /// <summary>
    /// Arabic-aware normalization: collapses tatweel and whitespace, unifies
    /// alef/yaa/taa-marbuta variants, lowercases Latin. Copied from
    /// ImportController.NormalizeForMatch so the matcher behaves the same
    /// way as the existing Visio-import pipeline.
    /// </summary>
    private static string NormalizeForMatch(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var sb = new System.Text.StringBuilder(s.Length);
        bool lastWasSpace = false;
        foreach (var ch in s.Trim())
        {
            char c = ch;
            if (c == 'ـ') continue;                                  // tatweel
            if (c == ' ' || char.IsWhiteSpace(c))
            {
                if (!lastWasSpace) { sb.Append(' '); lastWasSpace = true; }
                continue;
            }
            lastWasSpace = false;
            if (c == 'آ' || c == 'أ' || c == 'إ') c = 'ا'; // Alef forms
            else if (c == 'ى') c = 'ي';                              // Alef Maksura → Yaa
            else if (c == 'ة') c = 'ه';                              // Taa Marbuta → Haa
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static bool IsNormalizedSubstringMatch(string candidateNorm, string keyNorm) =>
        !string.IsNullOrEmpty(candidateNorm) && !string.IsNullOrEmpty(keyNorm)
        && (candidateNorm == keyNorm || candidateNorm.Contains(keyNorm) || keyNorm.Contains(candidateNorm));

    private static double LevenshteinRatio(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;
        int n = a.Length, m = b.Length;
        var d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;
        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        int max = Math.Max(n, m);
        return max == 0 ? 1.0 : 1.0 - ((double)d[n, m] / max);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Version management
    // ─────────────────────────────────────────────────────────────────────

    private async Task<int> NextVersionNumberAsync(string processId, CancellationToken ct)
    {
        var dbMax = await _db.ProcessBpmnVersions
            .Where(v => v.ProcessId == processId)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(ct) ?? 0;
        var localMax = _db.ProcessBpmnVersions.Local
            .Where(v => v.ProcessId == processId)
            .Select(v => v.VersionNumber)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(dbMax, localMax) + 1;
    }

    private async Task ArchiveExistingDiagramAsync(Models.APQC.Process process, string note, string? actingUserName, CancellationToken ct)
    {
        // Flip any IsCurrent version flags off; the new one will own that bit.
        var versions = await _db.ProcessBpmnVersions
            .Where(v => v.ProcessId == process.Id && v.IsCurrent)
            .ToListAsync(ct);
        foreach (var v in versions) v.IsCurrent = false;
    }
}
