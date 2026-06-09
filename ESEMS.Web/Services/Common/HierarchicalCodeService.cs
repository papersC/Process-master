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
    /// Re-stamps a Process's hierarchical <see cref="Process.Code"/> (and
    /// <see cref="Process.SortKey"/>) to the next free "{PG.Code}.{Z}" under its
    /// <em>current</em> <see cref="Process.ProcessGroupId"/>, then cascades the
    /// rename down the whole derived-code subtree: child Activities
    /// ("{procCode}.NN") and their Tasks ("{procCode}.NN.M"). The cascade is a
    /// pure prefix swap ("{oldCode}." → "{newCode}."), so any depth is handled
    /// uniformly; descendant codes that don't start with the old process code
    /// (hand-edited / non-conforming) are left untouched.
    ///
    /// Call this <b>after</b> re-parenting a process to a different ProcessGroup
    /// (set <see cref="Process.ProcessGroupId"/> first); without it the visible
    /// ID keeps pointing at the old group. The caller owns SaveChanges so the
    /// rename commits atomically with the rest of the edit. The process must
    /// already be loaded/tracked; descendant rows are loaded into the tracker
    /// here so their mutations are persisted by the same SaveChanges.
    /// </summary>
    public async Task RecodeProcessUnderGroupAsync(Process process, CancellationToken ct = default)
    {
        var oldCode = process.Code;
        var newCode = await NextProcessCodeAsync(process.ProcessGroupId, ct);

        process.Code = newCode;
        process.SortKey = SortKeyFor(newCode);

        // Nothing derived to fix if the process had no code yet or the code is
        // unchanged (e.g. the group's prefix happened to match).
        if (string.IsNullOrEmpty(oldCode) || oldCode == newCode)
            return;

        var oldPrefix = oldCode + ".";

        // Cascade to the whole subtree. Tasks hang off activities (ActivityId),
        // so load the process's activities first, then their tasks.
        var activities = await _db.Activities
            .Where(a => a.ProcessId == process.Id)
            .ToListAsync(ct);

        var activityIds = activities.Select(a => a.Id).ToList();
        var tasks = activityIds.Count == 0
            ? new List<ProcessTask>()
            : await _db.ProcessTasks
                .Where(t => activityIds.Contains(t.ActivityId))
                .ToListAsync(ct);

        // Swap the "{oldCode}." prefix for "{newCode}." on every descendant
        // whose code is genuinely derived from the process code. The trailing
        // dot in the match guards against false hits (e.g. sibling "1.1.30"
        // must not match prefix of "1.1.3").
        foreach (var act in activities)
            if (act.Code.StartsWith(oldPrefix, StringComparison.Ordinal))
                act.Code = newCode + act.Code.Substring(oldCode.Length);

        foreach (var task in tasks)
            if (task.Code.StartsWith(oldPrefix, StringComparison.Ordinal))
                task.Code = newCode + task.Code.Substring(oldCode.Length);
    }

    /// <summary>
    /// Re-stamps a ProcessGroup's hierarchical <see cref="ProcessGroup.Code"/>
    /// (and <see cref="ProcessGroup.SortKey"/>) to the next free "{Cat.Code}.{Y}"
    /// under its <em>current</em> <see cref="ProcessGroup.CategoryId"/>, then
    /// cascades the rename down its whole subtree: child Processes
    /// ("{groupCode}.Z" — SortKey recomputed too), their Activities and their
    /// Tasks.
    ///
    /// Unlike <see cref="RecodeProcessUnderGroupAsync"/> (which reallocates the
    /// moved process's own trailing segment), here the descendants <b>keep their
    /// own segments</b> — only the moved node changes number, so the cascade is a
    /// single prefix swap "{oldGroupCode}." → "{newGroupCode}." applied uniformly
    /// at every depth. Codes that don't start with the old group code
    /// (hand-edited / non-conforming) are left untouched.
    ///
    /// Call this <b>after</b> re-parenting a group to a different Category
    /// (set <see cref="ProcessGroup.CategoryId"/> first). The caller owns
    /// SaveChanges so the rename commits atomically; descendant rows are loaded
    /// into the tracker here so their mutations persist with the same save.
    /// </summary>
    public async Task RecodeProcessGroupUnderCategoryAsync(ProcessGroup processGroup, CancellationToken ct = default)
    {
        var oldCode = processGroup.Code;
        var newCode = await NextProcessGroupCodeAsync(processGroup.CategoryId, ct);

        processGroup.Code = newCode;
        processGroup.SortKey = SortKeyFor(newCode);

        if (string.IsNullOrEmpty(oldCode) || oldCode == newCode)
            return;

        var oldPrefix = oldCode + ".";

        // Load the subtree breadth-first: processes in this group, then their
        // activities, then those activities' tasks.
        var processes = await _db.Processes
            .Where(p => p.ProcessGroupId == processGroup.Id)
            .ToListAsync(ct);
        var processIds = processes.Select(p => p.Id).ToList();

        var activities = processIds.Count == 0
            ? new List<Activity>()
            : await _db.Activities
                .Where(a => processIds.Contains(a.ProcessId))
                .ToListAsync(ct);
        var activityIds = activities.Select(a => a.Id).ToList();

        var tasks = activityIds.Count == 0
            ? new List<ProcessTask>()
            : await _db.ProcessTasks
                .Where(t => activityIds.Contains(t.ActivityId))
                .ToListAsync(ct);

        // Prefix-swap across the whole subtree (trailing dot guards against
        // false hits, e.g. sibling group "1.30" vs "1.3"). Processes also get a
        // fresh SortKey since their Code changed.
        foreach (var p in processes)
            if (p.Code.StartsWith(oldPrefix, StringComparison.Ordinal))
            {
                p.Code = newCode + p.Code.Substring(oldCode.Length);
                p.SortKey = SortKeyFor(p.Code);
            }

        foreach (var act in activities)
            if (act.Code.StartsWith(oldPrefix, StringComparison.Ordinal))
                act.Code = newCode + act.Code.Substring(oldCode.Length);

        foreach (var task in tasks)
            if (task.Code.StartsWith(oldPrefix, StringComparison.Ordinal))
                task.Code = newCode + task.Code.Substring(oldCode.Length);
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
