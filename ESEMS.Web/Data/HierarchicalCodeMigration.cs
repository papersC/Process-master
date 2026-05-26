using System.Text.RegularExpressions;
using ESEMS.Web.Models.APQC;
using Microsoft.EntityFrameworkCore;

namespace ESEMS.Web.Data;

/// <summary>
/// Renumbers Categories / ProcessGroups / Processes into the strict
/// hierarchical scheme:
///   Category       => "1", "2", ...
///   ProcessGroup   => "{Cat}.{Y}"      e.g. "1.1"
///   Process        => "{PG}.{Z}"       e.g. "1.1.1"
///
/// Idempotent: any row whose Code already matches the new format keeps its
/// Code and is just (re)assigned a SortKey. On Processes, old code is
/// preserved in the LegacyCode column so external links keep resolving.
/// Category and ProcessGroup no longer carry LegacyCode/DisplayOrder.
///
/// MP-/SP- semantics: SubProcesses (LegacyCode starting "SP-") are linked
/// to the MainProcess (LegacyCode "MP-") in the same ProcessGroup via
/// ParentProcessId — but only when there's exactly one MP- in that group
/// (no ambiguity).
/// </summary>
public static class HierarchicalCodeMigration
{
    private static readonly Regex CategoryRx = new(@"^\d+$", RegexOptions.Compiled);
    private static readonly Regex ProcessGroupRx = new(@"^\d+\.\d+$", RegexOptions.Compiled);
    private static readonly Regex ProcessRx = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);

    public static async Task RunAsync(ApplicationDbContext db)
    {
        await MigrateCategoriesAsync(db);
        await MigrateProcessGroupsAsync(db);
        await MigrateProcessesAsync(db);
        await db.SaveChangesAsync();
    }

    // ---------- Categories: "1.0" → "1" ----------
    private static async Task MigrateCategoriesAsync(ApplicationDbContext db)
    {
        var categories = await db.Categories
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Code)
            .ToListAsync();

        var maxAssigned = categories
            .Where(c => CategoryRx.IsMatch(c.Code))
            .Select(c => int.TryParse(c.Code, out var n) ? n : 0)
            .DefaultIfEmpty(0).Max();

        foreach (var cat in categories)
        {
            if (CategoryRx.IsMatch(cat.Code))
            {
                cat.SortKey = ZeroPad(cat.Code);
                continue;
            }

            // "1.0" → "1": prefer the integer part; otherwise allocate next.
            var seg = cat.Code.Split('.').FirstOrDefault();
            if (int.TryParse(seg, out var n) && !categories.Any(c => c.Code == n.ToString() && c.Id != cat.Id))
            {
                cat.Code = n.ToString();
            }
            else
            {
                cat.Code = (++maxAssigned).ToString();
            }
            cat.SortKey = ZeroPad(cat.Code);
        }
    }

    // ---------- ProcessGroups: assign "{Cat}.{Y}" ----------
    private static async Task MigrateProcessGroupsAsync(ApplicationDbContext db)
    {
        // Categories already migrated in this transaction; re-read to pick up new codes.
        var categories = await db.Categories.Where(c => !c.IsDeleted).ToListAsync();
        var groups = await db.ProcessGroups
            .Where(pg => !pg.IsDeleted)
            .OrderBy(pg => pg.Code)
            .ToListAsync();

        // Per-category running counter for newly assigned suffixes.
        var perCatNext = new Dictionary<string, int>();

        // Pre-seed the counter from any group already in new format.
        foreach (var g in groups.Where(g => ProcessGroupRx.IsMatch(g.Code)))
        {
            var parts = g.Code.Split('.');
            if (int.TryParse(parts[1], out var y))
            {
                perCatNext.TryGetValue(parts[0], out var cur);
                perCatNext[parts[0]] = Math.Max(cur, y);
            }
            g.SortKey = ZeroPad(g.Code);
        }

        foreach (var g in groups.Where(g => !ProcessGroupRx.IsMatch(g.Code)))
        {
            var cat = categories.FirstOrDefault(c => c.Id == g.CategoryId);
            if (cat == null) continue;     // orphan — skip; will fix on next run.

            perCatNext.TryGetValue(cat.Code, out var nextY);
            nextY++;
            perCatNext[cat.Code] = nextY;

            g.Code = $"{cat.Code}.{nextY}";
            g.SortKey = ZeroPad(g.Code);
        }
    }

    // ---------- Processes: assign "{PG}.{Z}" + ParentProcessId for MP/SP ----------
    private static async Task MigrateProcessesAsync(ApplicationDbContext db)
    {
        var groups = await db.ProcessGroups.Where(pg => !pg.IsDeleted).ToListAsync();
        var processes = await db.Processes
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Code)
            .ToListAsync();

        var perGroupNext = new Dictionary<string, int>();

        // Pre-seed from already-migrated processes.
        foreach (var p in processes.Where(p => ProcessRx.IsMatch(p.Code)))
        {
            var parts = p.Code.Split('.');
            var prefix = $"{parts[0]}.{parts[1]}";
            if (int.TryParse(parts[2], out var z))
            {
                perGroupNext.TryGetValue(prefix, out var cur);
                perGroupNext[prefix] = Math.Max(cur, z);
            }
            p.SortKey = ZeroPad(p.Code);
        }

        // Process MP- and SP- legacy codes first so we can wire ParentProcessId.
        // Then PRC-/everything else gets a plain X.Y.Z.
        var pending = processes.Where(p => !ProcessRx.IsMatch(p.Code)).ToList();

        foreach (var p in pending)
        {
            var pg = groups.FirstOrDefault(g => g.Id == p.ProcessGroupId);
            if (pg == null) continue;

            perGroupNext.TryGetValue(pg.Code, out var nextZ);
            nextZ++;
            perGroupNext[pg.Code] = nextZ;

            p.LegacyCode ??= p.Code;
            p.Code = $"{pg.Code}.{nextZ}";
            p.SortKey = ZeroPad(p.Code);
        }

        // Now wire ParentProcessId: for each ProcessGroup that had exactly one
        // MP- legacy code, attach all SP- legacy codes in the same group as
        // children. Skip ambiguous groups (multiple MP-).
        var byGroup = processes
            .Where(p => !string.IsNullOrEmpty(p.LegacyCode))
            .GroupBy(p => p.ProcessGroupId);

        foreach (var grp in byGroup)
        {
            var mains = grp.Where(p => p.LegacyCode!.StartsWith("MP-", StringComparison.OrdinalIgnoreCase)).ToList();
            if (mains.Count != 1) continue;
            var parent = mains[0];
            var subs = grp.Where(p => p.LegacyCode!.StartsWith("SP-", StringComparison.OrdinalIgnoreCase));
            foreach (var sub in subs)
            {
                if (string.IsNullOrEmpty(sub.ParentProcessId))
                    sub.ParentProcessId = parent.Id;
            }
        }
    }

    /// <summary>
    /// Zero-pads each numeric segment of a hierarchical code to 4 digits so
    /// lexicographic sort matches numeric sort. e.g. "1.10.2" → "0001.0010.0002".
    /// </summary>
    public static string ZeroPad(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        return string.Join('.', code.Split('.').Select(seg =>
            int.TryParse(seg, out var n) ? n.ToString("D4") : seg));
    }
}
