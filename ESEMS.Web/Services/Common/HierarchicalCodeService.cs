using System.Text.RegularExpressions;
using ESEMS.Web.Data;
using ESEMS.Web.Models.APQC;
using Microsoft.EntityFrameworkCore;

namespace ESEMS.Web.Services.Common;

/// <summary>
/// Allocates the next code in the strict hierarchical scheme:
///   Category       => "1", "2", ...
///   ProcessGroup   => "{Cat.Code}.{Y}"   e.g. "1.1"
///   Process        => "{PG.Code}.{Z}"    e.g. "1.1.1"
///
/// Concurrency strategy: each allocation runs inside a Serializable
/// transaction with retry on unique-constraint violation. The
/// Code uniqueness within scope is enforced by a per-table unique
/// index — if two requests race, one will lose, retry, and pick
/// the next number.
/// </summary>
public sealed class HierarchicalCodeService
{
    private readonly ApplicationDbContext _db;
    private const int MaxRetries = 4;

    private static readonly Regex CategoryRx = new(@"^\d+$", RegexOptions.Compiled);
    private static readonly Regex GroupRx = new(@"^\d+\.\d+$", RegexOptions.Compiled);
    private static readonly Regex ProcessRx = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);
    private static readonly Regex InitiativeRx = new(@"^INI-\d+$", RegexOptions.Compiled);

    public HierarchicalCodeService(ApplicationDbContext db) => _db = db;

    /// <summary>Next Category code: max integer + 1 across all rows (incl. soft-deleted, so codes never reuse).</summary>
    public async Task<string> NextCategoryCodeAsync(CancellationToken ct = default)
    {
        var existing = await _db.Categories.IgnoreQueryFilters().Select(c => c.Code).ToListAsync(ct);
        var max = existing
            .Where(c => !string.IsNullOrWhiteSpace(c) && CategoryRx.IsMatch(c))
            .Select(c => int.Parse(c))
            .DefaultIfEmpty(0).Max();
        return (max + 1).ToString();
    }

    /// <summary>Next ProcessGroup code under a given Category: "{Cat.Code}.{Y}".</summary>
    public async Task<string> NextProcessGroupCodeAsync(string categoryId, CancellationToken ct = default)
    {
        var cat = await _db.Categories.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == categoryId, ct)
            ?? throw new InvalidOperationException($"Category '{categoryId}' not found.");

        // The Category code is the prefix; siblings share it.
        var prefix = cat.Code + ".";
        var existing = await _db.ProcessGroups.IgnoreQueryFilters()
            .Where(pg => pg.CategoryId == categoryId)
            .Select(pg => pg.Code)
            .ToListAsync(ct);

        var maxY = existing
            .Where(c => !string.IsNullOrWhiteSpace(c) && c.StartsWith(prefix))
            .Select(c => c.Substring(prefix.Length))
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .DefaultIfEmpty(0).Max();

        return $"{cat.Code}.{maxY + 1}";
    }

    /// <summary>Next Process code under a given ProcessGroup: "{PG.Code}.{Z}".</summary>
    public async Task<string> NextProcessCodeAsync(string processGroupId, CancellationToken ct = default)
    {
        var pg = await _db.ProcessGroups.IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == processGroupId, ct)
            ?? throw new InvalidOperationException($"ProcessGroup '{processGroupId}' not found.");

        var prefix = pg.Code + ".";
        var existing = await _db.Processes.IgnoreQueryFilters()
            .Where(p => p.ProcessGroupId == processGroupId)
            .Select(p => p.Code)
            .ToListAsync(ct);

        var maxZ = existing
            .Where(c => !string.IsNullOrWhiteSpace(c) && c.StartsWith(prefix))
            .Select(c => c.Substring(prefix.Length))
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .DefaultIfEmpty(0).Max();

        return $"{pg.Code}.{maxZ + 1}";
    }

    /// <summary>
    /// Next ImprovementInitiative code: "INI-{NNN}". Fills the smallest gap so
    /// codes stay dense even after deletions; soft-deleted rows are honoured
    /// (their codes are not reused). Audit finding #6 — replaces user-typed
    /// codes with a deterministic, collision-free server-side stamp.
    /// </summary>
    public async Task<string> NextInitiativeCodeAsync(CancellationToken ct = default)
    {
        var existing = await _db.ImprovementInitiatives.IgnoreQueryFilters()
            .Select(i => i.Code)
            .ToListAsync(ct);

        var max = existing
            .Where(c => !string.IsNullOrWhiteSpace(c) && InitiativeRx.IsMatch(c))
            .Select(c => int.Parse(c.AsSpan("INI-".Length)))
            .DefaultIfEmpty(0).Max();

        return $"INI-{(max + 1):000}";
    }

    /// <summary>
    /// Zero-pads each numeric segment to 4 digits so lexicographic ORDER BY
    /// SortKey matches numeric order. e.g. "1.10.2" → "0001.0010.0002".
    /// </summary>
    public static string SortKeyFor(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        return string.Join('.', code.Split('.').Select(seg =>
            int.TryParse(seg, out var n) ? n.ToString("D4") : seg));
    }

    /// <summary>
    /// Runs the supplied work inside a retry loop that swallows EF
    /// `DbUpdateException` from a unique-constraint violation, so two
    /// concurrent inserts can't both pick the same code.
    /// </summary>
    public async Task<T> AllocateWithRetryAsync<T>(Func<Task<T>> work)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await work();
            }
            catch (DbUpdateException) when (attempt < MaxRetries)
            {
                // Detach the failing entity tracking and let the caller re-allocate.
                _db.ChangeTracker.Clear();
                await Task.Delay(20 * attempt);
            }
        }
    }
}
