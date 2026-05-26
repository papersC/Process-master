namespace ESEMS.Web.Models.Import;

/// <summary>
/// One legacy-import run. Stores a JSON manifest of every row the run created
/// (table + id) so the import can be undone in one click from the Settings Hub
/// → Data tab. Hard-delete on revert frees the natural-key unique indexes
/// (AssetTag / Code) so the same file can be re-imported cleanly afterwards.
/// </summary>
public sealed class ImportBatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Importer kind, e.g. "mbrhe-assets", "mbrhe-apqc".</summary>
    public string Kind { get; set; } = string.Empty;

    public string? FileName { get; set; }

    /// <summary>Rows inserted (matches ImportResult.Imported).</summary>
    public int ImportedCount { get; set; }

    /// <summary>Rows skipped as duplicates.</summary>
    public int SkippedCount { get; set; }

    /// <summary>JSON array of { "t": tableName, "id": rowId } for every created row.</summary>
    public string Manifest { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedByName { get; set; }

    public bool IsReverted { get; set; }

    public DateTime? RevertedAt { get; set; }

    /// <summary>How many manifest rows were actually hard-deleted on revert.</summary>
    public int RevertedCount { get; set; }
}
