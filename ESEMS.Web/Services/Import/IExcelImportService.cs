namespace ESEMS.Web.Services.Import;

/// <summary>
/// Parses uploaded Excel files from the Administrator Hub → Data tab and
/// inserts the rows into the corresponding tables.
///
/// Semantics: insert-only. Rows whose natural key (Code / AssetTag /
/// RiskNumber) already exists are skipped — never overwritten. This keeps
/// re-uploads safe and matches the "do not change data" rule the upload UI
/// implies.
/// </summary>
public interface IExcelImportService
{
    Task<ImportResult> ImportProcessesAsync(Stream xlsx, CancellationToken ct = default);
    Task<ImportResult> ImportServicesAsync(Stream xlsx, CancellationToken ct = default);
    Task<ImportResult> ImportAssetsAsync(Stream xlsx, CancellationToken ct = default);
    Task<ImportResult> ImportRisksAsync(Stream xlsx, CancellationToken ct = default);

    /// <summary>
    /// Legacy MBRHE Asset Register import. Reads the 4-column client xlsx
    /// (Asset Name / Description / Classification / Owner — all Arabic) and
    /// maps it onto the canonical Asset entity: Classification → AssetCategory
    /// FK (Digital → AST-INFO-APP, Physical → AST-RE-PROJ), Owner department
    /// NameAr → OrganizationUnit FK, auto-generated AssetTag, housing-unit
    /// count parsed from name when present. NameEn falls back to NameAr so
    /// the BilingualEntity [Required] constraint passes.
    /// </summary>
    Task<ImportResult> ImportMbrheAssetRegisterAsync(Stream xlsx, CancellationToken ct = default);

    /// <summary>
    /// Legacy MBRHE Services Catalog import. Reads the 22-column client xlsx
    /// (sheet "MBRHE Services", Arabic+English bilingual headers on rows 2-3,
    /// data from row 4) and creates a Service plus its 1:1 ServiceCatalogInfo
    /// sidecar. Maps "Service Category (English)" to a ServiceCategory FK
    /// (auto-creates rows the client uses that aren't in the seed yet — e.g.
    /// "Complementary Services", "Dubai Municipality", "RTA"). Maps "Service
    /// Channels (English)" CSV to the ServiceDeliveryChannel enum where known.
    /// Parses "Fees (English)" for the "no fees" sentinel ⇒ IsFree=true;
    /// other narrative formats stay null and remain on the operations team's
    /// follow-up list.
    /// </summary>
    Task<ImportResult> ImportMbrheServicesCatalogAsync(Stream xlsx, CancellationToken ct = default);

    /// <summary>
    /// Legacy MBRHE Organizational Structure import. Two-sheet client xlsx:
    ///   sheet "الهيكل التنظيمي" — 38 units with parent-name linkage (single pass,
    ///   file is topologically ordered so parents come before children); the
    ///   "النوع" (Type) column maps to <see cref="ESEMS.Web.Models.Enums.OrganizationUnitType"/>.
    ///   sheet "المهام الوظيفية" — 283+ task rows in "group-by-first-named-unit"
    ///   format (unit name only on the first row of each group, blank on subsequent
    ///   rows). Each row creates one <see cref="ESEMS.Web.Models.APQC.OrganizationUnitResponsibility"/>
    ///   under the current unit. NameEn falls back to NameAr.
    /// Idempotent: skips by NameAr on units and (UnitId + first 200 chars of task) on responsibilities.
    /// </summary>
    Task<ImportResult> ImportMbrheOrgStructureAsync(Stream xlsx, CancellationToken ct = default);

    /// <summary>
    /// Legacy APQC Process Mapping import. Single sheet (~225 rows × 25 cols)
    /// of the denormalized 3-level APQC hierarchy with L1/L2 names repeated
    /// per L3 row. Each row creates (idempotent):
    ///   Category (L1) — upsert by NameEn
    ///   ProcessGroup (L2) — upsert by (CategoryId, NameEn)
    ///   Process (L3) — upsert by Code (taken from "Document Code" when present)
    /// Maps the Arabic categorical columns to enums: Classification → ProcessClassificationType,
    /// Automation Level → AutomationStatus, Automable → AutomabilityStatus,
    /// Current/Proposed → CurrentProposedStatus.
    /// Skips columns that need cross-entity lookups (Sector/Department/Section,
    /// Digital Systems, Linked Services, Government Partners, Strategic Objectives)
    /// — those become follow-up admin work; the import lays down the spine.
    /// </summary>
    Task<ImportResult> ImportApqcProcessMappingAsync(Stream xlsx, CancellationToken ct = default);

    /// <summary>
    /// "Replace" mode for an import kind: FK-safely deletes the existing data
    /// of the type about to be imported, so the upload replaces rather than
    /// appends. Supported kinds: "mbrhe-apqc" (full catalog — categories,
    /// groups, processes + children), "processes" (processes only, keeps
    /// groups/categories), "services"/"mbrhe-services", "assets"/"mbrhe-assets",
    /// "risks". Unsupported kinds (e.g. "mbrhe-org") are a no-op — the caller
    /// falls back to append (those importers are already idempotent). Runs in a
    /// transaction; throws on failure so the caller can abort before parsing.
    /// </summary>
    Task WipeForKindAsync(string kind, CancellationToken ct = default);

    /// <summary>
    /// Approved Job Titles (المسميات المعتمدة) import. 4-column client xlsx
    /// (Job Name / Job Name Arabic / Direct Org Name En / Direct Org Name Ar).
    /// Each row creates a JobPosition (the RACI role catalog, table JobRoles)
    /// linked to its OrganizationUnit by normalized Arabic name. Insert-only;
    /// idempotent on (NameAr, OrganizationUnitId). Unmatched org ⇒ a unit-less
    /// title plus a soft warning.
    /// </summary>
    Task<ImportResult> ImportJobTitlesAsync(Stream xlsx, CancellationToken ct = default);
}

public sealed class ImportResult
{
    public int Imported { get; set; }
    public int Skipped { get; set; }

    /// <summary>Hard failures — the row did NOT import. Sets success=false.</summary>
    public List<RowError> Errors { get; } = new();

    /// <summary>Soft issues — the row DID import but with a caveat (e.g. a
    /// cross-table dependency like an owner department or parent unit that
    /// wasn't found yet). Does NOT fail the import; surfaced so the user can
    /// fix the order and re-import.</summary>
    public List<RowError> Warnings { get; } = new();

    /// <summary>Set when the whole file can't be parsed (bad header, empty workbook).
    /// When non-null, Imported/Skipped are zero.</summary>
    public string? FatalError { get; set; }

    /// <summary>Every row this run actually inserted, as (logical table name, row id).
    /// Populated by the legacy importers so the run can be undone later. The
    /// table name must match the ApplicationDbContext DbSet (e.g. "Assets",
    /// "ServiceCatalogInfos") because the revert logic switches on it.</summary>
    public List<ImportedRef> Created { get; } = new();
}

public sealed record RowError(int Row, string Message);

public readonly record struct ImportedRef(string Table, string Id);
