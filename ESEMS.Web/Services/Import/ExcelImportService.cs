using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.AssetManagement;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.RiskManagement;
using ESEMS.Web.Models.Services;

namespace ESEMS.Web.Services.Import;

/// <summary>
/// Parses the four xlsx shapes downloaded from <c>/Import/Download*Template</c>
/// and inserts the rows into the matching tables. Insert-only — existing rows
/// (by Code / AssetTag / RiskNumber) are skipped, never updated.
///
/// The header set per kind is the authoritative contract between template
/// download and upload. If columns are added to a template, mirror them here.
/// </summary>
public sealed class ExcelImportService : IExcelImportService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ExcelImportService> _logger;

    // Expected headers per kind. The parser is case-insensitive but order is
    // not significant — columns are looked up by name.
    private static readonly string[] ProcessHeaders = new[]
    {
        "Code", "Name (English)", "Name (Arabic)", "Description (English)",
        "Description (Arabic)", "Process Group Code", "Status", "Process Type"
    };

    private static readonly string[] ServiceHeaders = new[]
    {
        "Code", "Name (English)", "Name (Arabic)", "Description (English)",
        "Description (Arabic)", "Service Type", "Owning Unit Code"
    };

    private static readonly string[] AssetHeaders = new[]
    {
        "Asset Tag", "Name (English)", "Name (Arabic)", "Description (English)",
        "Description (Arabic)", "Category Code", "Status", "Purchase Date",
        "Purchase Cost"
    };

    private static readonly string[] RiskHeaders = new[]
    {
        "Risk Number", "Name (English)", "Name (Arabic)", "Description (English)",
        "Description (Arabic)", "Category Code", "Likelihood (1-5)", "Impact (1-5)"
    };

    public ExcelImportService(ApplicationDbContext context, ILogger<ExcelImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════════════════
    // REPLACE-MODE WIPE — clears the existing data of an import kind so the
    // upload replaces rather than appends. FK-safe (validated against the
    // schema's cascade/restrict/set-null actions). Org is intentionally absent:
    // it's referenced by NO_ACTION FKs from kept lookups and its importer is
    // idempotent, so re-import-in-append is the safe path there.
    // ════════════════════════════════════════════════════════════════════
    public async Task WipeForKindAsync(string kind, CancellationToken ct = default)
    {
        var sql = (kind ?? "").Trim().ToLowerInvariant() switch
        {
            "mbrhe-apqc"                   => CatalogWipeSql(includeGroupsAndCategories: true),
            "processes"                    => CatalogWipeSql(includeGroupsAndCategories: false),
            "services" or "mbrhe-services" => "SET XACT_ABORT ON;\nDELETE FROM Services;",
            "assets" or "mbrhe-assets"     => "SET XACT_ABORT ON;\nDELETE FROM Assets;",
            "risks"                        => "SET XACT_ABORT ON;\nDELETE FROM EnterpriseRisks;",
            _                              => null    // org / unknown → no-op (append)
        };
        if (sql == null) return;

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        await _context.Database.ExecuteSqlRawAsync(sql, ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// FK-safe delete of the APQC catalog. Nulls the optional cross-references
    /// into Process, deletes the RESTRICT-blocked Activity/Task subtree and the
    /// CASCADE Process children, then the Processes themselves — plus, when
    /// <paramref name="includeGroupsAndCategories"/> is true, the ProcessGroups
    /// and Categories. (The per-process template import needs existing groups,
    /// so it wipes processes only; the full APQC import wipes all three levels.)
    /// </summary>
    private static string CatalogWipeSql(bool includeGroupsAndCategories)
    {
        var sql = @"SET XACT_ABORT ON;
UPDATE Assets                  SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;
UPDATE EnterpriseRisks         SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;
UPDATE Incidents               SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;
UPDATE Problems                SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;
UPDATE CustomerFeedbacks       SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;
UPDATE ChangeRequests          SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;
UPDATE ImprovementInitiatives  SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;
UPDATE ImprovementMeasurements SET AppliesToProcessId = NULL WHERE AppliesToProcessId IS NOT NULL;
UPDATE WorkloadLineItems       SET ProcessId          = NULL WHERE ProcessId          IS NOT NULL;
DELETE FROM TaskRacis;
DELETE FROM ProcessTasks;
DELETE FROM ActivityRacis;
DELETE FROM Activities;
DELETE FROM ProcessRacis;
DELETE FROM ProcessMeasurements;
DELETE FROM ProcessRisks;
DELETE FROM ProcessServices;
DELETE FROM ProcessResponsibilities;
DELETE FROM ProcessStrategicObjectives;
DELETE FROM ImprovementProcesses;
DELETE FROM ProcessDocuments;
DELETE FROM ProcessBpmnVersions;
DELETE FROM BpmnLanes;
DELETE FROM Processes;
";
        if (includeGroupsAndCategories)
            sql += "DELETE FROM ProcessGroups;\nDELETE FROM Categories;\n";
        return sql;
    }

    // ════════════════════════════════════════════════════════════════════
    // PROCESSES
    // ════════════════════════════════════════════════════════════════════
    public async Task<ImportResult> ImportProcessesAsync(Stream xlsx, CancellationToken ct = default)
    {
        var result = new ImportResult();
        var ws = OpenFirstWorksheet(xlsx, result);
        if (ws == null) return result;

        var cols = ReadHeaders(ws);
        if (!ValidateHeaders(cols, ProcessHeaders, result)) return result;

        // Pre-load lookups so each row is O(1)
        var groupByCode = await _context.ProcessGroups
            .AsNoTracking()
            .Where(g => !g.IsDeleted)
            .ToDictionaryAsync(g => g.Code, g => g.Id, StringComparer.OrdinalIgnoreCase, ct);
        var existingCodes = await _context.Processes
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .Select(p => p.Code)
            .ToListAsync(ct);
        var existingCodeSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<Process>();
        // Also track within-file dupes
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in DataRows(ws))
        {
            var code = ReadString(row, cols, "Code");
            var nameEn = ReadString(row, cols, "Name (English)");
            var nameAr = ReadString(row, cols, "Name (Arabic)");
            var descEn = ReadString(row, cols, "Description (English)");
            var descAr = ReadString(row, cols, "Description (Arabic)");
            var groupCode = ReadString(row, cols, "Process Group Code");
            var statusRaw = ReadString(row, cols, "Status");
            var typeRaw = ReadString(row, cols, "Process Type");

            if (string.IsNullOrWhiteSpace(code))
            { result.Errors.Add(new RowError(row.RowNumber(), "Code is required.")); continue; }
            if (!ValidateName(nameEn, "English", row.RowNumber(), result)) continue;
            if (!ValidateName(nameAr, "Arabic", row.RowNumber(), result)) continue;

            if (existingCodeSet.Contains(code) || !seenInFile.Add(code))
            { result.Skipped++; continue; }

            if (string.IsNullOrWhiteSpace(groupCode))
            { result.Errors.Add(new RowError(row.RowNumber(), "Process Group Code is required.")); continue; }
            if (!groupByCode.TryGetValue(groupCode, out var groupId))
            { result.Errors.Add(new RowError(row.RowNumber(), $"Process Group '{groupCode}' not found.")); continue; }

            if (!TryParseEnum<ProcessStatus>(statusRaw, ProcessStatus.Draft, out var status, out var statusErr))
            { result.Errors.Add(new RowError(row.RowNumber(), $"Status: {statusErr}")); continue; }
            if (!TryParseEnum<ProcessType>(typeRaw, ProcessType.Core, out var type, out var typeErr))
            { result.Errors.Add(new RowError(row.RowNumber(), $"Process Type: {typeErr}")); continue; }

            toAdd.Add(new Process
            {
                Id = Guid.NewGuid().ToString(),
                Code = code!,
                NameEn = nameEn!,
                NameAr = nameAr!,
                DescriptionEn = descEn,
                DescriptionAr = descAr,
                ProcessGroupId = groupId,
                Status = status,
                ProcessType = type
            });
        }

        if (toAdd.Count > 0)
        {
            _context.Processes.AddRange(toAdd);
            await _context.SaveChangesAsync(ct);
            result.Imported = toAdd.Count;
            // F-024: record inserted rows so this template import is undoable
            // from "Recent imports" (RevertImport keeps any process later
            // referenced by an Activity).
            foreach (var p in toAdd) result.Created.Add(new("Processes", p.Id));
        }

        return result;
    }

    // ════════════════════════════════════════════════════════════════════
    // SERVICES
    // ════════════════════════════════════════════════════════════════════
    public async Task<ImportResult> ImportServicesAsync(Stream xlsx, CancellationToken ct = default)
    {
        var result = new ImportResult();
        var ws = OpenFirstWorksheet(xlsx, result);
        if (ws == null) return result;

        var cols = ReadHeaders(ws);
        if (!ValidateHeaders(cols, ServiceHeaders, result)) return result;

        var unitByCode = await _context.OrganizationUnits
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .ToDictionaryAsync(u => u.Code, u => u.Id, StringComparer.OrdinalIgnoreCase, ct);
        // ServiceCategory is OPTIONAL on import — empty cell ⇒ leave FK null.
        // Unknown code ⇒ row error (no auto-create; keep the lookup authoritative).
        var categoryByCode = await _context.ServiceCategories
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .ToDictionaryAsync(c => c.Code, c => c.Id, StringComparer.OrdinalIgnoreCase, ct);
        var existingCodes = await _context.Services
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .Select(s => s.Code)
            .ToListAsync(ct);
        var existingCodeSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<Service>();
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in DataRows(ws))
        {
            var code = ReadString(row, cols, "Code");
            var nameEn = ReadString(row, cols, "Name (English)");
            var nameAr = ReadString(row, cols, "Name (Arabic)");
            var descEn = ReadString(row, cols, "Description (English)");
            var descAr = ReadString(row, cols, "Description (Arabic)");
            var typeRaw = ReadString(row, cols, "Service Type");
            var unitCode = ReadString(row, cols, "Owning Unit Code");
            var catCode = ReadString(row, cols, "Service Category Code");

            if (string.IsNullOrWhiteSpace(code))
            { result.Errors.Add(new RowError(row.RowNumber(), "Code is required.")); continue; }
            if (!ValidateName(nameEn, "English", row.RowNumber(), result)) continue;
            if (!ValidateName(nameAr, "Arabic", row.RowNumber(), result)) continue;

            if (existingCodeSet.Contains(code) || !seenInFile.Add(code))
            { result.Skipped++; continue; }

            if (!TryParseEnum<ServiceType>(typeRaw, ServiceType.External, out var type, out var typeErr))
            { result.Errors.Add(new RowError(row.RowNumber(), $"Service Type: {typeErr}")); continue; }

            int? unitId = null;
            if (!string.IsNullOrWhiteSpace(unitCode))
            {
                if (!unitByCode.TryGetValue(unitCode, out var resolved))
                { result.Errors.Add(new RowError(row.RowNumber(), $"Owning Unit '{unitCode}' not found.")); continue; }
                unitId = resolved;
            }

            string? categoryId = null;
            if (!string.IsNullOrWhiteSpace(catCode))
            {
                if (!categoryByCode.TryGetValue(catCode, out var resolvedCat))
                { result.Errors.Add(new RowError(row.RowNumber(), $"Service Category '{catCode}' not found.")); continue; }
                categoryId = resolvedCat;
            }

            toAdd.Add(new Service
            {
                Id = Guid.NewGuid().ToString(),
                Code = code!,
                NameEn = nameEn!,
                NameAr = nameAr!,
                DescriptionEn = descEn,
                DescriptionAr = descAr,
                ServiceType = type,
                OwningUnitId = unitId,
                ServiceCategoryId = categoryId
            });
        }

        if (toAdd.Count > 0)
        {
            _context.Services.AddRange(toAdd);
            await _context.SaveChangesAsync(ct);
            result.Imported = toAdd.Count;
            // F-024: record inserted rows so this template import is undoable.
            foreach (var s in toAdd) result.Created.Add(new("Services", s.Id));
        }

        return result;
    }

    // ════════════════════════════════════════════════════════════════════
    // ASSETS
    // ════════════════════════════════════════════════════════════════════
    public async Task<ImportResult> ImportAssetsAsync(Stream xlsx, CancellationToken ct = default)
    {
        var result = new ImportResult();
        var ws = OpenFirstWorksheet(xlsx, result);
        if (ws == null) return result;

        var cols = ReadHeaders(ws);
        if (!ValidateHeaders(cols, AssetHeaders, result)) return result;

        var categoryByCode = await _context.AssetCategories
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .ToDictionaryAsync(c => c.Code, c => c.Id, StringComparer.OrdinalIgnoreCase, ct);
        var existingTags = await _context.Assets
            .AsNoTracking()
            .Where(a => !a.IsDeleted)
            .Select(a => a.AssetTag)
            .ToListAsync(ct);
        var existingTagSet = new HashSet<string>(existingTags, StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<Asset>();
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in DataRows(ws))
        {
            var tag = ReadString(row, cols, "Asset Tag");
            var nameEn = ReadString(row, cols, "Name (English)");
            var nameAr = ReadString(row, cols, "Name (Arabic)");
            var descEn = ReadString(row, cols, "Description (English)");
            var descAr = ReadString(row, cols, "Description (Arabic)");
            var catCode = ReadString(row, cols, "Category Code");
            var statusRaw = ReadString(row, cols, "Status");

            if (string.IsNullOrWhiteSpace(tag))
            { result.Errors.Add(new RowError(row.RowNumber(), "Asset Tag is required.")); continue; }
            if (!ValidateName(nameEn, "English", row.RowNumber(), result)) continue;
            if (!ValidateName(nameAr, "Arabic", row.RowNumber(), result)) continue;

            if (existingTagSet.Contains(tag) || !seenInFile.Add(tag))
            { result.Skipped++; continue; }

            if (string.IsNullOrWhiteSpace(catCode))
            { result.Errors.Add(new RowError(row.RowNumber(), "Category Code is required.")); continue; }
            if (!categoryByCode.TryGetValue(catCode, out var catId))
            { result.Errors.Add(new RowError(row.RowNumber(), $"Asset Category '{catCode}' not found.")); continue; }

            if (!TryParseEnum<AssetStatus>(statusRaw, AssetStatus.Planned, out var status, out var statusErr))
            { result.Errors.Add(new RowError(row.RowNumber(), $"Status: {statusErr}")); continue; }

            if (!TryReadDate(row, cols, "Purchase Date", out var purchaseDate, out var dateErr))
            { result.Errors.Add(new RowError(row.RowNumber(), $"Purchase Date: {dateErr}")); continue; }
            if (!TryReadDecimal(row, cols, "Purchase Cost", out var purchaseCost, out var costErr))
            { result.Errors.Add(new RowError(row.RowNumber(), $"Purchase Cost: {costErr}")); continue; }

            toAdd.Add(new Asset
            {
                Id = Guid.NewGuid().ToString(),
                AssetTag = tag!,
                NameEn = nameEn!,
                NameAr = nameAr!,
                DescriptionEn = descEn,
                DescriptionAr = descAr,
                CategoryId = catId,
                Status = status,
                PurchaseDate = purchaseDate,
                PurchaseCost = purchaseCost
            });
        }

        if (toAdd.Count > 0)
        {
            _context.Assets.AddRange(toAdd);
            await _context.SaveChangesAsync(ct);
            result.Imported = toAdd.Count;
            // F-024: record inserted rows so this template import is undoable.
            foreach (var a in toAdd) result.Created.Add(new("Assets", a.Id));
        }

        return result;
    }

    // ════════════════════════════════════════════════════════════════════
    // RISKS
    // ════════════════════════════════════════════════════════════════════
    public async Task<ImportResult> ImportRisksAsync(Stream xlsx, CancellationToken ct = default)
    {
        var result = new ImportResult();
        var ws = OpenFirstWorksheet(xlsx, result);
        if (ws == null) return result;

        var cols = ReadHeaders(ws);
        if (!ValidateHeaders(cols, RiskHeaders, result)) return result;

        var categoryByCode = await _context.RiskCategories
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .ToDictionaryAsync(c => c.Code, c => c.Id, StringComparer.OrdinalIgnoreCase, ct);
        var existingNumbers = await _context.EnterpriseRisks
            .AsNoTracking()
            .Where(r => !r.IsDeleted)
            .Select(r => r.RiskNumber)
            .ToListAsync(ct);
        var existingNumberSet = new HashSet<string>(existingNumbers, StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<EnterpriseRisk>();
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in DataRows(ws))
        {
            var number = ReadString(row, cols, "Risk Number");
            var nameEn = ReadString(row, cols, "Name (English)");
            var nameAr = ReadString(row, cols, "Name (Arabic)");
            var descEn = ReadString(row, cols, "Description (English)");
            var descAr = ReadString(row, cols, "Description (Arabic)");
            var catCode = ReadString(row, cols, "Category Code");

            if (string.IsNullOrWhiteSpace(number))
            { result.Errors.Add(new RowError(row.RowNumber(), "Risk Number is required.")); continue; }
            if (!ValidateName(nameEn, "English", row.RowNumber(), result)) continue;
            if (!ValidateName(nameAr, "Arabic", row.RowNumber(), result)) continue;

            if (existingNumberSet.Contains(number) || !seenInFile.Add(number))
            { result.Skipped++; continue; }

            if (string.IsNullOrWhiteSpace(catCode))
            { result.Errors.Add(new RowError(row.RowNumber(), "Category Code is required.")); continue; }
            if (!categoryByCode.TryGetValue(catCode, out var catId))
            { result.Errors.Add(new RowError(row.RowNumber(), $"Risk Category '{catCode}' not found.")); continue; }

            if (!TryReadInt(row, cols, "Likelihood (1-5)", out var likelihood, out var lErr) || likelihood is < 1 or > 5)
            { result.Errors.Add(new RowError(row.RowNumber(), $"Likelihood must be 1-5{(lErr != null ? $" ({lErr})" : "")}.")); continue; }
            if (!TryReadInt(row, cols, "Impact (1-5)", out var impact, out var iErr) || impact is < 1 or > 5)
            { result.Errors.Add(new RowError(row.RowNumber(), $"Impact must be 1-5{(iErr != null ? $" ({iErr})" : "")}.")); continue; }

            var risk = new EnterpriseRisk
            {
                Id = Guid.NewGuid().ToString(),
                RiskNumber = number!,
                NameEn = nameEn!,
                NameAr = nameAr!,
                DescriptionEn = descEn,
                DescriptionAr = descAr,
                CategoryId = catId,
                Likelihood = likelihood!.Value,
                Impact = impact!.Value
            };
            risk.CalculateInherentRiskScore();
            toAdd.Add(risk);
        }

        if (toAdd.Count > 0)
        {
            _context.EnterpriseRisks.AddRange(toAdd);
            await _context.SaveChangesAsync(ct);
            result.Imported = toAdd.Count;
            // F-024: record inserted rows so this template import is undoable
            // (RevertImport handles the "EnterpriseRisks" table name).
            foreach (var r in toAdd) result.Created.Add(new("EnterpriseRisks", r.Id));
        }

        return result;
    }

    // ════════════════════════════════════════════════════════════════════
    // MBRHE LEGACY ASSET REGISTER (4-column client xlsx)
    //   Asset Name (Ar) | Description (Ar) | Classification | Owner / Department
    // Classification ∈ { "Digital", "Physical" }; owner is Arabic dept name
    // or the literal "MBRHE" for the root. Maps onto the canonical Asset
    // entity with sensible defaults and an auto-generated AssetTag.
    // ════════════════════════════════════════════════════════════════════
    private static readonly string[] MbrheAssetHeaders = new[]
    {
        "Asset Name", "Description", "Classification", "Owner / Department"
    };

    public async Task<ImportResult> ImportMbrheAssetRegisterAsync(Stream xlsx, CancellationToken ct = default)
    {
        var result = new ImportResult();
        var ws = OpenFirstWorksheet(xlsx, result);
        if (ws == null) return result;

        var cols = ReadHeaders(ws);
        if (!ValidateHeaders(cols, MbrheAssetHeaders, result)) return result;

        // Resolve the two default categories used by this importer.
        var realEstateCat = await _context.AssetCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == "AST-RE-PROJ" && !c.IsDeleted, ct);
        var infoAssetCat = await _context.AssetCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == "AST-INFO-APP" && !c.IsDeleted, ct);
        if (realEstateCat == null || infoAssetCat == null)
        {
            result.FatalError = "Asset categories AST-RE-PROJ and/or AST-INFO-APP not found. "
                + "Run the asset-category seed before importing.";
            return result;
        }

        // Pre-load org units by Arabic name (case-insensitive). The xlsx
        // column 4 uses Arabic dept names; "MBRHE" is the corporate root
        // and maps to no specific unit (AssignedToUnitId stays null).
        var unitByNameAr = await _context.OrganizationUnits.AsNoTracking()
            .Where(u => !u.IsDeleted)
            .ToListAsync(ct);
        var unitLookup = unitByNameAr
            .Where(u => !string.IsNullOrWhiteSpace(u.NameAr))
            .GroupBy(u => u.NameAr.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        // Existing AssetTags so we can pick a non-colliding next number per
        // prefix (RE-### for real-estate, INFO-### for digital).
        var existingTags = await _context.Assets.AsNoTracking()
            .Where(a => !a.IsDeleted)
            .Select(a => a.AssetTag)
            .ToListAsync(ct);
        var nextRe = NextSeq(existingTags, "RE-");
        var nextInfo = NextSeq(existingTags, "INFO-");

        // Existing asset names (NameAr) so duplicate uploads are skipped.
        var existingNamesAr = new HashSet<string>(
            await _context.Assets.AsNoTracking()
                .Where(a => !a.IsDeleted)
                .Select(a => a.NameAr)
                .ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<Asset>();
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // F-023: count rows that name a specific (non-MBRHE) owner department we
        // couldn't resolve. Combined with an empty org table below, this detects
        // the "imported Assets before the Org Structure" mistake.
        var unresolvedSpecificOwners = 0;

        foreach (var row in DataRows(ws))
        {
            var nameAr = ReadString(row, cols, "Asset Name");
            var descAr = ReadString(row, cols, "Description");
            var classification = (ReadString(row, cols, "Classification") ?? "").Trim();
            var ownerAr = (ReadString(row, cols, "Owner / Department") ?? "").Trim();

            if (string.IsNullOrWhiteSpace(nameAr))
            { result.Errors.Add(new RowError(row.RowNumber(), "Asset Name is required.")); continue; }
            nameAr = nameAr.Trim();

            if (existingNamesAr.Contains(nameAr) || !seenInFile.Add(nameAr))
            { result.Skipped++; continue; }

            bool isDigital = classification.Equals("Digital", StringComparison.OrdinalIgnoreCase);
            bool isPhysical = classification.Equals("Physical", StringComparison.OrdinalIgnoreCase);
            if (!isDigital && !isPhysical)
            {
                result.Errors.Add(new RowError(row.RowNumber(),
                    $"Classification must be 'Digital' or 'Physical' — got '{classification}'."));
                continue;
            }

            var categoryId = isDigital ? infoAssetCat.Id : realEstateCat.Id;
            var assetTag = isDigital ? $"INFO-{nextInfo++:D3}" : $"RE-{nextRe++:D3}";

            // Owner: "MBRHE" → corporate root (no specific unit). Other Arabic
            // names look up against OrganizationUnit.NameAr. Unknown owners
            // produce a row warning but don't fail the row — the asset is
            // still imported, just unassigned.
            int? unitId = null;
            if (!string.IsNullOrWhiteSpace(ownerAr)
                && !ownerAr.Equals("MBRHE", StringComparison.OrdinalIgnoreCase))
            {
                if (!unitLookup.TryGetValue(ownerAr, out var resolvedUnitId))
                {
                    unresolvedSpecificOwners++;
                    result.Warnings.Add(new RowError(row.RowNumber(),
                        $"Owner department '{ownerAr}' not found — asset imported unassigned. "
                        + "Import Org Structure first, then undo this and re-import to link it."));
                }
                else
                {
                    unitId = resolvedUnitId;
                }
            }

            var asset = new Asset
            {
                Id = Guid.NewGuid().ToString(),
                AssetTag = assetTag,
                NameEn = nameAr,                  // fallback — satisfies [Required]; refine later
                NameAr = nameAr,
                DescriptionEn = null,
                DescriptionAr = string.IsNullOrWhiteSpace(descAr) ? null : descAr.Trim(),
                CategoryId = categoryId,
                AssignedToUnitId = unitId,
                Status = ESEMS.Web.Models.Enums.AssetStatus.Operational,
                Criticality = 3
            };

            if (isPhysical)
            {
                // Parse housing-unit count out of the asset name when present.
                // Examples that should hit: "344 مساكن حتا" → 344,
                // "(20) مسكن" → 20, "8 مساكن - الليسيلي" → 8.
                var unitCount = ParseHousingUnitCount(nameAr);
                if (unitCount.HasValue) asset.Units = unitCount.Value;
                // Existing housing portfolio in the legacy register is in-service;
                // tag as Occupied (already with residents) which is the closest
                // match in ConstructionStatus. Admin can refine post-import.
                asset.ConstructionStatus = ESEMS.Web.Models.Enums.ConstructionStatus.Occupied;
            }
            else
            {
                // Digital → information asset. Pull a coarse platform name
                // out of the description into StorageSystem so dashboards
                // can group by Oracle / TRIRIGA / ARIS / etc.
                var platform = DetectInfoAssetPlatform(descAr);
                if (platform != null) asset.StorageSystem = platform;
                // Default classification = Internal; client can refine.
                asset.Classification = ESEMS.Web.Models.Enums.InformationClassification.Internal;
            }

            toAdd.Add(asset);
        }

        // F-023: enforce import order. If the org table is completely empty and
        // the file names specific owner departments, the user imported Assets
        // before the Org Structure — every asset would land unassigned. Fail the
        // whole run (nothing saved) with a clear instruction instead of silently
        // creating a pile of unlinked assets. When some org units DO exist, a few
        // unresolved owners stay soft warnings (partial import is intentional).
        if (unitLookup.Count == 0 && unresolvedSpecificOwners > 0)
        {
            result.FatalError =
                "No organization units exist yet, so every asset's owner department is unresolved. "
                + "Import the Org Structure first, then import Assets so each asset links to its department.";
            return result;
        }

        if (toAdd.Count > 0)
        {
            _context.Assets.AddRange(toAdd);
            await _context.SaveChangesAsync(ct);
            result.Imported = toAdd.Count;
            foreach (var a in toAdd) result.Created.Add(new("Assets", a.Id));
        }

        return result;
    }

    /// <summary>
    /// Extracts a unit count from the Arabic project name. Three-stage match:
    /// (1) explicit "(N) مسكن" or "N مساكن/شقة/شقق" — preferred when present
    ///     because the keyword anchors the number's meaning;
    /// (2) bare numeric token anywhere in the name — required because most
    ///     MBRHE entries omit the keyword (e.g. "البرشاء الثالثة 223",
    ///     "اللسيلي 9", "هور العنز شرق 11"). Pick the largest numeric token
    ///     in the housing-portfolio range to avoid mistaking a project
    ///     ordinal ("الثالثة 3") for unit count.
    /// Returns null when no plausible number is found.
    /// </summary>
    private static int? ParseHousingUnitCount(string name)
    {
        // (N) مسكن / مساكن / شقة / شقق
        var m = System.Text.RegularExpressions.Regex.Match(name,
            @"\((\d{1,4})\)\s*(?:مسكن|مساكن|شقة|شقق)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var paren)) return paren;

        // N مسكن / مساكن / شقة / شقق (number directly before the keyword)
        m = System.Text.RegularExpressions.Regex.Match(name,
            @"(?<!\d)(\d{1,4})\s*(?:مسكن|مساكن|شقة|شقق)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var pre)) return pre;

        // No keyword present — fall back to the largest plausible numeric
        // token in the string. Housing-portfolio sizes are 1–2000 units;
        // anything outside that band is almost certainly an address /
        // year / phone fragment and gets ignored.
        var matches = System.Text.RegularExpressions.Regex.Matches(name, @"\d{1,4}");
        int best = 0;
        foreach (System.Text.RegularExpressions.Match mm in matches)
        {
            if (int.TryParse(mm.Value, out var n) && n is >= 1 and <= 2000 && n > best)
                best = n;
        }
        return best > 0 ? best : (int?)null;
    }

    /// <summary>
    /// Coarse platform/tech detection from the Arabic description (which
    /// often embeds the English platform name). Returns null if nothing
    /// recognisable is present.
    /// </summary>
    private static string? DetectInfoAssetPlatform(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var d = description;
        // Order matters: longer/more-specific patterns first.
        if (Contains(d, "Apex Oracle") || Contains(d, "Oracle Apex") || Contains(d, "Oracle Application")) return "Oracle Apex";
        if (Contains(d, "Microsoft Dynamic CRM") || Contains(d, "Dynamics CRM")) return "Microsoft Dynamics CRM";
        if (Contains(d, "IBM")) return "IBM";
        if (Contains(d, "TRIRIGA")) return "IBM TRIRIGA";
        if (Contains(d, "ARIS")) return "ARIS";
        if (Contains(d, "SPIDER")) return "SPIDER";
        if (Contains(d, "Jira")) return "Atlassian Jira";
        if (Contains(d, "Metaverse")) return "Metaverse";
        if (Contains(d, "bankplus")) return "bankplus";
        if (Contains(d, ".Net") || Contains(d, ".NET")) return ".NET";
        if (Contains(d, "Java")) return "Java";
        if (Contains(d, "Oracle")) return "Oracle";
        return null;

        static bool Contains(string s, string token)
            => s.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Highest numeric suffix already used for a given AssetTag prefix
    /// (e.g. existing "RE-007" + "RE-009" → returns 10 as the next free).
    /// </summary>
    private static int NextSeq(IEnumerable<string> existingTags, string prefix)
    {
        var max = existingTags
            .Where(t => t != null && t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(t =>
            {
                var tail = t.Substring(prefix.Length);
                return int.TryParse(tail, out var n) ? n : 0;
            })
            .DefaultIfEmpty(0)
            .Max();
        return max + 1;
    }

    // ════════════════════════════════════════════════════════════════════
    // MBRHE SERVICES CATALOG (22-column bilingual client xlsx)
    //   Sheet: "MBRHE Services"
    //   Row 1: file title (skip), Row 2: Arabic headers, Row 3: English headers,
    //   Row 4+: data. Reading anchored on the English-header row.
    // Each row → one Service + one ServiceCatalogInfo sidecar.
    // ════════════════════════════════════════════════════════════════════
    private static readonly string[] MbrheServiceHeadersEn = new[]
    {
        "Name (Arabic)", "Name (English)",
        "Description (Arabic)", "Description (English)",
        "Service Category (Arabic)", "Service Category (English)"
    };

    public async Task<ImportResult> ImportMbrheServicesCatalogAsync(Stream xlsx, CancellationToken ct = default)
    {
        var result = new ImportResult();

        ClosedXML.Excel.XLWorkbook workbook;
        try { workbook = new ClosedXML.Excel.XLWorkbook(xlsx); }
        catch (Exception ex) { result.FatalError = "Cannot open workbook: " + ex.Message; return result; }
        using (workbook)
        {
            var ws = workbook.Worksheets.FirstOrDefault(w => string.Equals(w.Name, "MBRHE Services", StringComparison.OrdinalIgnoreCase))
                  ?? workbook.Worksheets.FirstOrDefault();
            if (ws == null) { result.FatalError = "Workbook has no sheets."; return result; }

            // Header is row 3 (English). The file's row 1 is a merged title,
            // row 2 is Arabic headers — both skipped.
            const int headerRow = 3;
            const int firstDataRow = 4;

            var cols = ReadHeadersFromRow(ws, headerRow);
            if (!ValidateHeaders(cols, MbrheServiceHeadersEn, result)) return result;

            var categoriesByEn = await _context.ServiceCategories.AsNoTracking()
                .Where(c => !c.IsDeleted)
                .ToListAsync(ct);
            var categoryLookup = categoriesByEn
                .GroupBy(c => c.NameEn.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var nextCategorySeq = NextSeq(categoriesByEn.Select(c => c.Code), "SC-");

            var existingServiceCodes = await _context.Services.AsNoTracking()
                .Where(s => !s.IsDeleted)
                .Select(s => s.Code)
                .ToListAsync(ct);
            var nextSvcSeq = NextSeq(existingServiceCodes, "SVC-");

            var existingServiceNamesAr = new HashSet<string>(
                await _context.Services.AsNoTracking()
                    .Where(s => !s.IsDeleted)
                    .Select(s => s.NameAr)
                    .ToListAsync(ct),
                StringComparer.OrdinalIgnoreCase);

            var servicesToAdd = new List<Service>();
            var catalogToAdd = new List<ServiceCatalogInfo>();
            var newCategories = new List<ServiceCategory>();
            var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            for (int rowNum = firstDataRow; rowNum <= lastRow; rowNum++)
            {
                var row = ws.Row(rowNum);
                if (row.IsEmpty()) continue;

                var nameAr = ReadCell(row, cols, "Name (Arabic)");
                var nameEn = ReadCell(row, cols, "Name (English)");
                if (string.IsNullOrWhiteSpace(nameAr) && string.IsNullOrWhiteSpace(nameEn))
                    continue;
                if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
                {
                    result.Errors.Add(new RowError(rowNum, "Both Name (Arabic) and Name (English) are required."));
                    continue;
                }
                nameAr = nameAr.Trim();
                nameEn = nameEn.Trim();

                if (existingServiceNamesAr.Contains(nameAr) || !seenInFile.Add(nameAr))
                { result.Skipped++; continue; }

                // Resolve category — by English name first, then Arabic.
                // Auto-create when missing because the file is the client's
                // canonical taxonomy and we want zero rejected rows.
                var catEn = ReadCell(row, cols, "Service Category (English)")?.Trim();
                var catAr = ReadCell(row, cols, "Service Category (Arabic)")?.Trim();
                ServiceCategory? category = null;
                if (!string.IsNullOrWhiteSpace(catEn))
                {
                    if (!categoryLookup.TryGetValue(catEn, out category))
                    {
                        category = new ServiceCategory
                        {
                            Id = Guid.NewGuid().ToString(),
                            Code = $"SC-{nextCategorySeq++:D3}",
                            NameEn = catEn,
                            NameAr = string.IsNullOrWhiteSpace(catAr) ? catEn : catAr,
                            DisplayOrder = (categoriesByEn.Count + newCategories.Count + 1) * 10,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        newCategories.Add(category);
                        categoryLookup[catEn] = category;
                    }
                }

                var service = new Service
                {
                    Id = Guid.NewGuid().ToString(),
                    Code = $"SVC-{nextSvcSeq++:D4}",
                    NameEn = nameEn,
                    NameAr = nameAr,
                    DescriptionEn = ReadCell(row, cols, "Description (English)")?.Trim(),
                    DescriptionAr = ReadCell(row, cols, "Description (Arabic)")?.Trim(),
                    ServiceCategoryId = category?.Id,
                    ServiceType = ESEMS.Web.Models.Enums.ServiceType.External,
                    Channel = ESEMS.Web.Models.Enums.ChannelType.Hybrid,
                    IsActive = true,
                    DisplayOrder = (servicesToAdd.Count + 1) * 10
                };
                servicesToAdd.Add(service);

                // Build the 1:1 catalog sidecar. Narrative fields are copied
                // straight; structured Fee + Channels are parsed where the
                // text is recognisable.
                var feeEn = ReadCell(row, cols, "Fees (English)") ?? "";
                var (isFree, feeAmount, feeNote) = ParseFee(feeEn);
                var channelsEn = ReadCell(row, cols, "Service Channels (English)") ?? "";
                var channels = ParseDeliveryChannels(channelsEn);

                var catalog = new ServiceCatalogInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceId = service.Id,
                    TargetAudienceEn = ReadCell(row, cols, "Target Audience (English)")?.Trim(),
                    TargetAudienceAr = ReadCell(row, cols, "Target Audience (Arabic)")?.Trim(),
                    PreConditionsEn = ReadCell(row, cols, "Pre-Conditions (English)")?.Trim(),
                    PreConditionsAr = ReadCell(row, cols, "Pre-Conditions (Arabic)")?.Trim(),
                    PoliciesEn = ReadCell(row, cols, "Policies (English)")?.Trim(),
                    PoliciesAr = ReadCell(row, cols, "Policies (Arabic)")?.Trim(),
                    ProcedureEn = ReadCell(row, cols, "Service Procedure (English)")?.Trim(),
                    ProcedureAr = ReadCell(row, cols, "Service Procedure (Arabic)")?.Trim(),
                    IsFree = isFree,
                    FeeAmount = feeAmount,
                    FeeNote = feeNote,
                    SourceReference = "MBRHE_Services_Catalog_Populated.xlsx",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                if (channels.Count > 0) catalog.SetDeliveryChannelList(channels);
                catalogToAdd.Add(catalog);
            }

            if (newCategories.Count > 0)
                _context.ServiceCategories.AddRange(newCategories);
            if (servicesToAdd.Count > 0)
                _context.Services.AddRange(servicesToAdd);
            if (catalogToAdd.Count > 0)
                _context.ServiceCatalogInfos.AddRange(catalogToAdd);

            if (servicesToAdd.Count > 0 || newCategories.Count > 0)
            {
                await _context.SaveChangesAsync(ct);
                result.Imported = servicesToAdd.Count;
                foreach (var c in newCategories) result.Created.Add(new("ServiceCategories", c.Id));
                foreach (var s in servicesToAdd) result.Created.Add(new("Services", s.Id));
                foreach (var ci in catalogToAdd) result.Created.Add(new("ServiceCatalogInfos", ci.Id));
            }
        }
        return result;
    }

    /// <summary>
    /// Reads English-side headers off a specific row (the client file uses
    /// row 1 = title, row 2 = Arabic headers, row 3 = English headers).
    /// </summary>
    private static Dictionary<string, int> ReadHeadersFromRow(ClosedXML.Excel.IXLWorksheet ws, int rowNumber)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = ws.Row(rowNumber);
        foreach (var cell in headerRow.CellsUsed())
        {
            var name = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(name) && !headers.ContainsKey(name))
                headers[name] = cell.Address.ColumnNumber;
        }
        return headers;
    }

    /// <summary>Reads a cell by header name; returns null when the column
    /// doesn't exist or the cell is blank.</summary>
    private static string? ReadCell(ClosedXML.Excel.IXLRow row, Dictionary<string, int> cols, string header)
    {
        if (!cols.TryGetValue(header, out var col)) return null;
        var v = row.Cell(col).GetString();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    /// <summary>
    /// Parses the "Fees (English)" cell into a structured (IsFree, Amount, Note).
    /// Recognises the "no fees" sentinel; extracts the first AED amount from
    /// anything else and stores the narrative as FeeNote (capped at 200 chars).
    /// </summary>
    private static (bool isFree, decimal? amount, string? note) ParseFee(string raw)
    {
        var s = raw.Trim();
        if (s.Length == 0) return (false, null, null);
        if (System.Text.RegularExpressions.Regex.IsMatch(s,
                @"\b(no\s+fees?|free)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return (true, null, null);
        var m = System.Text.RegularExpressions.Regex.Match(s,
            @"(\d+(?:[.,]\d+)?)\s*(?:AED|aed|د\.?إ)");
        decimal? amount = null;
        if (m.Success && decimal.TryParse(m.Groups[1].Value.Replace(",", ""),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var dec))
            amount = dec;
        var note = s.Length > 200 ? s.Substring(0, 200) : s;
        return (false, amount, note);
    }

    /// <summary>
    /// Parses "Service Channels (English)" CSV-ish narrative into a list of
    /// ServiceDeliveryChannel enum values. Unknown tokens are silently
    /// dropped — that's standard for legacy imports.
    /// </summary>
    private static List<ESEMS.Web.Models.Enums.ServiceDeliveryChannel> ParseDeliveryChannels(string raw)
    {
        var result = new List<ESEMS.Web.Models.Enums.ServiceDeliveryChannel>();
        if (string.IsNullOrWhiteSpace(raw)) return result;
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = token.Trim();
            if (t.Contains("Dubai Now", StringComparison.OrdinalIgnoreCase))
                result.Add(ESEMS.Web.Models.Enums.ServiceDeliveryChannel.DubaiNowApp);
            else if (t.Equals("Mobile", StringComparison.OrdinalIgnoreCase)
                  || t.Contains("Mobile App", StringComparison.OrdinalIgnoreCase))
                result.Add(ESEMS.Web.Models.Enums.ServiceDeliveryChannel.MobileApp);
            else if (t.Contains("Website", StringComparison.OrdinalIgnoreCase)
                  || t.Contains("Web Portal", StringComparison.OrdinalIgnoreCase)
                  || t.Contains("Online", StringComparison.OrdinalIgnoreCase))
                result.Add(ESEMS.Web.Models.Enums.ServiceDeliveryChannel.WebPortal);
            else if (t.Contains("Service Center", StringComparison.OrdinalIgnoreCase)
                  || t.Contains("Branch", StringComparison.OrdinalIgnoreCase)
                  || t.Contains("In-Person", StringComparison.OrdinalIgnoreCase))
                result.Add(ESEMS.Web.Models.Enums.ServiceDeliveryChannel.ServiceCenter);
            else if (t.Contains("Phone", StringComparison.OrdinalIgnoreCase)
                  || t.Contains("Call Center", StringComparison.OrdinalIgnoreCase))
                result.Add(ESEMS.Web.Models.Enums.ServiceDeliveryChannel.PhoneCall);
            else if (t.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase))
                result.Add(ESEMS.Web.Models.Enums.ServiceDeliveryChannel.WhatsApp);
            else if (t.Contains("SmartGov", StringComparison.OrdinalIgnoreCase)
                  || t.Contains("Smart Gov", StringComparison.OrdinalIgnoreCase)
                  || t.Contains("u.ae", StringComparison.OrdinalIgnoreCase))
                result.Add(ESEMS.Web.Models.Enums.ServiceDeliveryChannel.SmartGovPortal);
        }
        return result.Distinct().ToList();
    }

    // ════════════════════════════════════════════════════════════════════
    // MBRHE ORG STRUCTURE + FUNCTIONAL TASKS (Arabic-only client xlsx)
    //   Sheet 1 "الهيكل التنظيمي" — 38 units with parent-name linkage
    //   Sheet 2 "المهام الوظيفية" — 283+ tasks grouped under each unit
    // Imports both in one pass. Idempotent on re-upload.
    // ════════════════════════════════════════════════════════════════════
    private const string OrgSheetName = "الهيكل التنظيمي";
    private const string TasksSheetName = "المهام الوظيفية";

    public async Task<ImportResult> ImportMbrheOrgStructureAsync(Stream xlsx, CancellationToken ct = default)
    {
        var result = new ImportResult();

        ClosedXML.Excel.XLWorkbook workbook;
        try { workbook = new ClosedXML.Excel.XLWorkbook(xlsx); }
        catch (Exception ex) { result.FatalError = "Cannot open workbook: " + ex.Message; return result; }
        using (workbook)
        {
            var orgSheet = workbook.Worksheets.FirstOrDefault(w => w.Name == OrgSheetName);
            var tasksSheet = workbook.Worksheets.FirstOrDefault(w => w.Name == TasksSheetName);
            if (orgSheet == null || tasksSheet == null)
            {
                result.FatalError = $"Workbook must contain both '{OrgSheetName}' and '{TasksSheetName}' sheets.";
                return result;
            }

            // Pre-load existing org units (skip-duplicates by NameAr).
            var existingUnits = await _context.OrganizationUnits.AsNoTracking()
                .Where(u => !u.IsDeleted)
                .ToListAsync(ct);
            var unitsByNameAr = existingUnits
                .Where(u => !string.IsNullOrWhiteSpace(u.NameAr))
                .GroupBy(u => u.NameAr.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var existingCodes = existingUnits.Select(u => u.Code).ToList();
            var nextCode = NextSeq(existingCodes, "OU-");

            // ── PASS 1: org units (single linear pass; file is topologically
            // ordered so parents land before children). Headers row 2; data row 3+.
            var addedUnits = new List<OrganizationUnit>();
            int orgRowsLast = orgSheet.LastRowUsed()?.RowNumber() ?? 0;
            for (int rowNum = 3; rowNum <= orgRowsLast; rowNum++)
            {
                var row = orgSheet.Row(rowNum);
                if (row.IsEmpty()) continue;

                var nameAr = TrimOrNull(row.Cell(3).GetString());     // col 3 — الوحدة التنظيمية
                var parentAr = TrimOrNull(row.Cell(4).GetString());   // col 4 — تتبع لـ
                var typeAr = TrimOrNull(row.Cell(5).GetString());     // col 5 — النوع
                if (string.IsNullOrEmpty(nameAr)) continue;

                if (unitsByNameAr.ContainsKey(nameAr))
                { result.Skipped++; continue; }

                // Resolve parent via NAVIGATION property — the int identity key
                // isn't known until SaveChanges. EF assigns identities in insert
                // order and the file is topologically ordered (parents before
                // children), so the FK resolves on save.
                OrganizationUnit? parentObject = null;
                int? parentFkId = null;
                if (!string.IsNullOrEmpty(parentAr) && parentAr != "-")
                {
                    if (unitsByNameAr.TryGetValue(parentAr, out var parent))
                    {
                        // EXISTING (already-saved) parent → set the int FK directly.
                        // Do NOT set the navigation to it: it's a detached AsNoTracking
                        // entity, and AddRange would mark the whole reachable graph as
                        // Added and try to re-insert the existing parent with its
                        // explicit identity unit_id (SQL error 544). A NEW in-batch
                        // parent (Id still 0) uses the navigation so EF wires the FK
                        // once the parent receives its identity on save.
                        if (parent.Id != 0) parentFkId = parent.Id;
                        else parentObject = parent;
                    }
                    else
                        result.Warnings.Add(new RowError(rowNum,
                            $"Parent unit '{parentAr}' not found — unit created as a root. "
                            + "Check the file lists parents before their children."));
                }

                var unit = new OrganizationUnit
                {
                    // Id is a DB identity — never set client-side.
                    Code = $"OU-{nextCode++:D3}",
                    NameAr = nameAr,
                    NameEn = nameAr,    // fallback — English translation arrives later
                    Parent = parentObject,
                    ParentId = parentFkId,
                    UnitType = MapUnitType(typeAr),  // raw Arabic type string from column "النوع"
                    Level = 1,           // computed below in second pass once tree is known
                    DisplayOrder = (rowNum - 2) * 10,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                addedUnits.Add(unit);
                unitsByNameAr[nameAr] = unit;   // visible to children later in the same pass
            }

            if (addedUnits.Count > 0)
            {
                _context.OrganizationUnits.AddRange(addedUnits);
                await _context.SaveChangesAsync(ct);
                result.Imported += addedUnits.Count;
                // Identities now assigned — u.Id is int, ImportedRef.Id is string.
                foreach (var u in addedUnits) result.Created.Add(new("OrganizationUnits", u.Id.ToString()));

                // Compute Level (depth in tree) for the rows we added. EF fixup
                // populated each unit's int ParentId from the Parent navigation
                // during the save above, so we can walk up by FK exactly as
                // before (now int) — this also follows pre-existing ancestors.
                foreach (var unit in addedUnits)
                {
                    int level = 0;
                    var p = unit.ParentId;
                    while (p != null && level < 10)
                    {
                        var parent = await _context.OrganizationUnits.FindAsync(new object?[] { p.Value }, ct);
                        if (parent == null) break;
                        level++;
                        p = parent.ParentId;
                    }
                    unit.Level = level;
                }
                await _context.SaveChangesAsync(ct);
            }

            // ── PASS 2: tasks → OrganizationUnitResponsibility.
            // Grouping rule: column 2 (الوحدة التنظيمية) is populated on the first
            // row of a unit's task block, blank thereafter. Track the most recent
            // named unit and attach each task to it.
            var responsibilitiesToAdd = new List<OrganizationUnitResponsibility>();
            string? currentUnitNameAr = null;
            OrganizationUnit? currentUnitObject = null;
            int seqInGroup = 0;

            // Existing responsibilities for skip-duplicate check (keyed on unitId + task prefix).
            var existingResp = await _context.OrganizationUnitResponsibilities.AsNoTracking()
                .Where(r => !r.IsDeleted)
                .Select(r => new { r.OrganizationUnitId, Key = r.NameAr })
                .ToListAsync(ct);
            var existingRespSet = new HashSet<string>(
                existingResp.Select(r => r.OrganizationUnitId + "|" + Trunc(r.Key, 200)),
                StringComparer.OrdinalIgnoreCase);

            int taskRowsLast = tasksSheet.LastRowUsed()?.RowNumber() ?? 0;
            for (int rowNum = 3; rowNum <= taskRowsLast; rowNum++)
            {
                var row = tasksSheet.Row(rowNum);
                if (row.IsEmpty()) continue;

                var unitAr = TrimOrNull(row.Cell(2).GetString());   // col 2 — الوحدة التنظيمية
                var taskAr = TrimOrNull(row.Cell(4).GetString());   // col 4 — المهام الوظيفية

                if (!string.IsNullOrEmpty(unitAr))
                {
                    currentUnitNameAr = unitAr;
                    seqInGroup = 0;
                    if (!unitsByNameAr.TryGetValue(unitAr, out var unit))
                    {
                        currentUnitObject = null;
                        result.Errors.Add(new RowError(rowNum,
                            $"Unit '{unitAr}' not found in OrganizationUnits — task rows skipped."));
                    }
                    else
                    {
                        currentUnitObject = unit;
                    }
                }

                if (string.IsNullOrEmpty(taskAr) || currentUnitObject == null) continue;

                // Units added this run already have their int Id assigned (saved
                // above); pre-existing units carry their stored Id. Either way the
                // dedup key uses the resolved int Id.
                var dedupKey = currentUnitObject.Id + "|" + Trunc(taskAr, 200);
                if (!existingRespSet.Add(dedupKey)) continue;   // already present

                seqInGroup++;
                responsibilitiesToAdd.Add(new OrganizationUnitResponsibility
                {
                    Id = Guid.NewGuid().ToString(),
                    // Set the int FK directly (every unit has its Id by now). Do NOT
                    // use the navigation: currentUnitObject may be a detached
                    // AsNoTracking unit, and AddRange would try to re-track it,
                    // conflicting with units already tracked from pass 1.
                    OrganizationUnitId = currentUnitObject.Id,
                    NameAr = taskAr,
                    NameEn = taskAr,           // fallback
                    DisplayOrder = seqInGroup * 10,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            if (responsibilitiesToAdd.Count > 0)
            {
                _context.OrganizationUnitResponsibilities.AddRange(responsibilitiesToAdd);
                await _context.SaveChangesAsync(ct);
                result.Imported += responsibilitiesToAdd.Count;
                foreach (var r in responsibilitiesToAdd) result.Created.Add(new("OrganizationUnitResponsibilities", r.Id));
            }
        }
        return result;
    }

    /// <summary>
    /// Normalizes the Arabic "النوع" column to the unit-type string stored on
    /// <see cref="OrganizationUnit.UnitType"/>. UnitType is now free text (was
    /// the OrganizationUnitType enum), so we keep the raw Arabic value, trimmed.
    /// </summary>
    private static string? MapUnitType(string? ar) => TrimOrNull(ar);

    private static string? TrimOrNull(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Trunc(string s, int max)
        => s.Length > max ? s.Substring(0, max) : s;

    // ════════════════════════════════════════════════════════════════════
    // APQC PROCESS MAPPING (denormalized 3-level hierarchy client xlsx)
    //   Sheet "APQC Process Mapping" — row 1 group headers, row 2 column
    //   headers, row 3+ data. Each row = one L3 Process with L1/L2 names
    //   repeated. Reading by column index because the headers carry
    //   newline-separated bilingual labels.
    // Column layout (1-indexed):
    //    1 L1 Ar | 2 L1 En | 3 Classification | 4 L2 Ar | 5 L2 En
    //    6 L2 Desc | 7 L3 Ar | 8 L3 En | 9 L3 Desc | 10 Procedure Status
    //   11 Current/Proposed | 12 Document Code | 13–18 Sector/Dept/Section
    //   19 Automation Level | 20 Digital Systems | 21 Automable
    //   22 Links to Service flag | 23 Linked Services | 24 Gov Partners
    //   25 Strategic Objectives
    // ════════════════════════════════════════════════════════════════════
    public async Task<ImportResult> ImportApqcProcessMappingAsync(Stream xlsx, CancellationToken ct = default)
    {
        var result = new ImportResult();
        ClosedXML.Excel.XLWorkbook workbook;
        try { workbook = new ClosedXML.Excel.XLWorkbook(xlsx); }
        catch (Exception ex) { result.FatalError = "Cannot open workbook: " + ex.Message; return result; }
        using (workbook)
        {
            var ws = workbook.Worksheets.FirstOrDefault(w =>
                string.Equals(w.Name, "APQC Process Mapping", StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.FirstOrDefault();
            if (ws == null) { result.FatalError = "Workbook has no sheets."; return result; }

            // Pre-load existing rows (skip-duplicate keys).
            var existingCategories = await _context.Categories.AsNoTracking()
                .Where(c => !c.IsDeleted).ToListAsync(ct);
            var existingGroups = await _context.ProcessGroups.AsNoTracking()
                .Where(g => !g.IsDeleted).ToListAsync(ct);
            var existingProcesses = await _context.Processes.AsNoTracking()
                .Where(p => !p.IsDeleted)
                .Select(p => new { p.Code, p.LegacyCode, p.ProcessGroupId, p.NameEn })
                .ToListAsync(ct);

            var catByEn = existingCategories
                .GroupBy(c => c.NameEn.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var groupByCatAndEn = existingGroups
                .GroupBy(g => g.CategoryId + "|" + g.NameEn.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // ── Hierarchical-code counters (APQC X.Y.Z scheme), seeded from any
            // existing rows so an import continues the sequence instead of
            // colliding. Mirrors HierarchicalCodeMigration:
            //   Category "1","2"… | ProcessGroup "{cat}.{Y}" | Process "{group}.{Z}".
            // The Excel sheet's own document code is NOT used as the Code — it is
            // preserved in Process.LegacyCode instead.
            int nextCatNum = existingCategories
                .Select(c => int.TryParse(c.Code, out var n) ? n : 0)
                .DefaultIfEmpty(0).Max();

            var perCatNextGroup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in existingGroups)
            {
                var parts = g.Code.Split('.');
                if (parts.Length == 2 && int.TryParse(parts[1], out var y))
                {
                    perCatNextGroup.TryGetValue(parts[0], out var cur);
                    perCatNextGroup[parts[0]] = Math.Max(cur, y);
                }
            }

            var perGroupNextProc = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in existingProcesses)
            {
                var parts = p.Code.Split('.');
                if (parts.Length == 3 && int.TryParse(parts[2], out var z))
                {
                    var prefix = parts[0] + "." + parts[1];
                    perGroupNextProc.TryGetValue(prefix, out var cur);
                    perGroupNextProc[prefix] = Math.Max(cur, z);
                }
            }

            // Process re-import dedup: by document code when the sheet supplies one
            // (now stored in LegacyCode), else by (group, English name).
            var existingLegacy = new HashSet<string>(
                existingProcesses.Where(p => !string.IsNullOrEmpty(p.LegacyCode)).Select(p => p.LegacyCode!),
                StringComparer.OrdinalIgnoreCase);
            var existingGroupName = new HashSet<string>(
                existingProcesses.Select(p => p.ProcessGroupId + "|" + p.NameEn),
                StringComparer.OrdinalIgnoreCase);
            var seenLegacy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenGroupName = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var catsToAdd = new List<Category>();
            var groupsToAdd = new List<ProcessGroup>();
            var processesToAdd = new List<Process>();

            // Carry-forward state for the denormalized L1/L2 fill-down (see loop).
            string? lastL1Ar = null, lastL1En = null, lastL2Ar = null, lastL2En = null;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            for (int rowNum = 3; rowNum <= lastRow; rowNum++)
            {
                var row = ws.Row(rowNum);
                if (row.IsEmpty()) continue;

                var l1Ar = TrimOrNull(row.Cell(1).GetString());
                var l1En = TrimOrNull(row.Cell(2).GetString());
                var classification = TrimOrNull(row.Cell(3).GetString());
                var l2Ar = TrimOrNull(row.Cell(4).GetString());
                var l2En = TrimOrNull(row.Cell(5).GetString());
                var l2Desc = TrimOrNull(row.Cell(6).GetString());
                var l3Ar = TrimOrNull(row.Cell(7).GetString());
                var l3En = TrimOrNull(row.Cell(8).GetString());
                var l3Desc = TrimOrNull(row.Cell(9).GetString());
                var currentProposed = TrimOrNull(row.Cell(11).GetString());
                var docCode = TrimOrNull(row.Cell(12).GetString());
                var automationLevel = TrimOrNull(row.Cell(19).GetString());
                var automable = TrimOrNull(row.Cell(21).GetString());

                // The client file is denormalized with fill-down: L1/L2 names are
                // written only on the first row of each block and left blank on the
                // continuation rows beneath. Carry the last-seen L1/L2 forward so
                // every L3 procedure attaches to the right category/group.
                if (l1Ar != null || l1En != null) { lastL1Ar = l1Ar; lastL1En = l1En; }
                else { l1Ar = lastL1Ar; l1En = lastL1En; }
                if (l2Ar != null || l2En != null) { lastL2Ar = l2Ar; lastL2En = l2En; }
                else { l2Ar = lastL2Ar; l2En = lastL2En; }

                // Bilingual fallback when only one side is filled — the same rule
                // the other legacy importers use (NameEn falls back to NameAr).
                l1En ??= l1Ar; l1Ar ??= l1En;
                l2En ??= l2Ar; l2Ar ??= l2En;
                l3En ??= l3Ar; l3Ar ??= l3En;

                // No L3 procedure on this row → not a process row; skip quietly.
                if (l3En == null) continue;

                // After carry-forward a procedure must still resolve to L1 + L2.
                if (l1En == null || l2En == null)
                {
                    result.Errors.Add(new RowError(rowNum,
                        "Procedure has no category/group to attach to (L1/L2 blank with nothing to carry forward); row skipped."));
                    continue;
                }

                // ── L1 Category — upsert by NameEn
                if (!catByEn.TryGetValue(l1En, out var category))
                {
                    var catCode = (++nextCatNum).ToString();
                    category = new Category
                    {
                        Id = Guid.NewGuid().ToString(),
                        Code = catCode,
                        SortKey = HierarchicalCodeMigration.ZeroPad(catCode),
                        NameEn = l1En,
                        NameAr = l1Ar,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    catsToAdd.Add(category);
                    catByEn[l1En] = category;
                    perCatNextGroup[catCode] = 0;
                }

                // ── L2 ProcessGroup — upsert by (CategoryId, NameEn)
                var pgKey = category.Id + "|" + l2En;
                if (!groupByCatAndEn.TryGetValue(pgKey, out var group))
                {
                    perCatNextGroup.TryGetValue(category.Code, out var y);
                    y++;
                    perCatNextGroup[category.Code] = y;
                    var pgCode = $"{category.Code}.{y}";
                    group = new ProcessGroup
                    {
                        Id = Guid.NewGuid().ToString(),
                        Code = pgCode,
                        SortKey = HierarchicalCodeMigration.ZeroPad(pgCode),
                        CategoryId = category.Id,
                        NameEn = l2En,
                        NameAr = l2Ar,
                        DescriptionEn = null,
                        DescriptionAr = l2Desc,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    groupsToAdd.Add(group);
                    groupByCatAndEn[pgKey] = group;
                    perGroupNextProc[pgCode] = 0;
                }

                // ── L3 Process — skip re-imports, then assign a hierarchical
                // code "{group}.{Z}". The sheet's own document code (when present)
                // is preserved in LegacyCode, never used as the Code.
                if (!string.IsNullOrEmpty(docCode))
                {
                    if (existingLegacy.Contains(docCode) || !seenLegacy.Add(docCode))
                    { result.Skipped++; continue; }
                }
                else
                {
                    var nameKey = group.Id + "|" + l3En;
                    if (existingGroupName.Contains(nameKey) || !seenGroupName.Add(nameKey))
                    { result.Skipped++; continue; }
                }

                perGroupNextProc.TryGetValue(group.Code, out var z);
                z++;
                perGroupNextProc[group.Code] = z;
                var prCode = $"{group.Code}.{z}";

                var process = new Process
                {
                    Id = Guid.NewGuid().ToString(),
                    Code = prCode,
                    SortKey = HierarchicalCodeMigration.ZeroPad(prCode),
                    LegacyCode = docCode,
                    NameEn = l3En,
                    NameAr = l3Ar,
                    DescriptionEn = null,
                    DescriptionAr = l3Desc,
                    ProcessGroupId = group.Id,
                    ProcessType = MapClassificationToProcessType(classification),
                    ClassificationType = MapClassificationType(classification),
                    Status = ESEMS.Web.Models.Enums.ProcessStatus.Active,
                    AutomationStatus = MapAutomationStatus(automationLevel),
                    AutomabilityStatus = MapAutomabilityStatus(automable),
                    CurrentProposedStatus = MapCurrentProposed(currentProposed),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                processesToAdd.Add(process);
            }

            if (catsToAdd.Count > 0) _context.Categories.AddRange(catsToAdd);
            if (groupsToAdd.Count > 0) _context.ProcessGroups.AddRange(groupsToAdd);
            if (processesToAdd.Count > 0) _context.Processes.AddRange(processesToAdd);
            if (catsToAdd.Count + groupsToAdd.Count + processesToAdd.Count > 0)
            {
                await _context.SaveChangesAsync(ct);
                foreach (var c in catsToAdd) result.Created.Add(new("Categories", c.Id));
                foreach (var g in groupsToAdd) result.Created.Add(new("ProcessGroups", g.Id));
                foreach (var p in processesToAdd) result.Created.Add(new("Processes", p.Id));
            }

            result.Imported = catsToAdd.Count + groupsToAdd.Count + processesToAdd.Count;
        }
        return result;
    }

    private static ESEMS.Web.Models.Enums.ProcessType MapClassificationToProcessType(string? ar) => ar switch
    {
        "رئيسية"  => ESEMS.Web.Models.Enums.ProcessType.Core,
        "داعمة"   => ESEMS.Web.Models.Enums.ProcessType.Support,
        "ممكنة"   => ESEMS.Web.Models.Enums.ProcessType.Management,
        _ => ESEMS.Web.Models.Enums.ProcessType.Core
    };

    private static ESEMS.Web.Models.Enums.ProcessClassificationType MapClassificationType(string? ar) => ar switch
    {
        "رئيسية"  => ESEMS.Web.Models.Enums.ProcessClassificationType.Main,
        "داعمة"   => ESEMS.Web.Models.Enums.ProcessClassificationType.Support,
        "ممكنة"   => ESEMS.Web.Models.Enums.ProcessClassificationType.Enabling,
        _ => ESEMS.Web.Models.Enums.ProcessClassificationType.Main
    };

    private static ESEMS.Web.Models.Enums.AutomationStatus MapAutomationStatus(string? ar) => ar switch
    {
        "مؤتمتة"        => ESEMS.Web.Models.Enums.AutomationStatus.Automated,
        "شبه مؤتمتة"    => ESEMS.Web.Models.Enums.AutomationStatus.SemiAutomated,
        "تقليدية" or "يدوية" => ESEMS.Web.Models.Enums.AutomationStatus.Traditional,
        _ => ESEMS.Web.Models.Enums.AutomationStatus.Traditional
    };

    private static ESEMS.Web.Models.Enums.AutomabilityStatus MapAutomabilityStatus(string? ar) => ar switch
    {
        "قابل للأتمتة"              => ESEMS.Web.Models.Enums.AutomabilityStatus.Automatable,
        "قابل للأتمتة بشكل جزئي"    => ESEMS.Web.Models.Enums.AutomabilityStatus.PartiallyAutomatable,
        "غير قابل للأتمتة" or "لا ينطبق" => ESEMS.Web.Models.Enums.AutomabilityStatus.NotAutomatable,
        _ => ESEMS.Web.Models.Enums.AutomabilityStatus.NotAutomatable
    };

    private static ESEMS.Web.Models.Enums.CurrentProposedStatus MapCurrentProposed(string? ar) => ar switch
    {
        "إجراء حالي" => ESEMS.Web.Models.Enums.CurrentProposedStatus.Current,
        "مقترح"      => ESEMS.Web.Models.Enums.CurrentProposedStatus.Proposed,
        _ => ESEMS.Web.Models.Enums.CurrentProposedStatus.Current
    };

    // ════════════════════════════════════════════════════════════════════
    // Shared helpers
    // ════════════════════════════════════════════════════════════════════

    private IXLWorksheet? OpenFirstWorksheet(Stream xlsx, ImportResult result)
    {
        try
        {
            var wb = new XLWorkbook(xlsx);
            var ws = wb.Worksheets.FirstOrDefault();
            if (ws == null) { result.FatalError = "Workbook contains no worksheets."; return null; }
            return ws;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open xlsx for import");
            result.FatalError = "File is not a valid xlsx workbook.";
            return null;
        }
    }

    private static Dictionary<string, int> ReadHeaders(IXLWorksheet ws)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = ws.FirstRowUsed();
        if (headerRow == null) return headers;
        foreach (var cell in headerRow.CellsUsed())
        {
            var name = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(name) && !headers.ContainsKey(name))
                headers[name] = cell.Address.ColumnNumber;
        }
        return headers;
    }

    private static bool ValidateHeaders(Dictionary<string, int> actual, string[] expected, ImportResult result)
    {
        var missing = expected.Where(h => !actual.ContainsKey(h)).ToList();
        if (missing.Count == 0) return true;
        result.FatalError = "Missing required columns: " + string.Join(", ", missing);
        return false;
    }

    /// <summary>Yields each data row (row 2..last used) that has at least one
    /// non-empty cell. Empty rows are silently skipped.</summary>
    private static IEnumerable<IXLRow> DataRows(IXLWorksheet ws)
    {
        var range = ws.RangeUsed();
        if (range == null) yield break;
        var firstRow = range.FirstRow().RowNumber();
        var lastRow = range.LastRow().RowNumber();
        for (int rowNum = firstRow + 1; rowNum <= lastRow; rowNum++)
        {
            var row = ws.Row(rowNum);
            if (row.IsEmpty()) continue;
            yield return row;
        }
    }

    private static string? ReadString(IXLRow row, Dictionary<string, int> cols, string header)
    {
        if (!cols.TryGetValue(header, out var col)) return null;
        var v = row.Cell(col).GetString().Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private static bool TryReadInt(IXLRow row, Dictionary<string, int> cols, string header, out int? value, out string? error)
    {
        value = null; error = null;
        if (!cols.TryGetValue(header, out var col)) return true;
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return true;
        if (cell.TryGetValue<int>(out var n)) { value = n; return true; }
        var raw = cell.GetString().Trim();
        if (int.TryParse(raw, out n)) { value = n; return true; }
        error = $"'{raw}' is not an integer";
        return false;
    }

    private static bool TryReadDecimal(IXLRow row, Dictionary<string, int> cols, string header, out decimal? value, out string? error)
    {
        value = null; error = null;
        if (!cols.TryGetValue(header, out var col)) return true;
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return true;
        if (cell.TryGetValue<decimal>(out var n)) { value = n; return true; }
        var raw = cell.GetString().Trim();
        if (decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out n))
        { value = n; return true; }
        error = $"'{raw}' is not a number";
        return false;
    }

    private static bool TryReadDate(IXLRow row, Dictionary<string, int> cols, string header, out DateTime? value, out string? error)
    {
        value = null; error = null;
        if (!cols.TryGetValue(header, out var col)) return true;
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return true;
        if (cell.TryGetValue<DateTime>(out var d)) { value = d; return true; }
        var raw = cell.GetString().Trim();
        if (DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d))
        { value = d; return true; }
        error = $"'{raw}' is not a date";
        return false;
    }

    /// <summary>Parses an enum case-insensitively. Empty input falls back to
    /// <paramref name="defaultValue"/> (the same default the entity uses).</summary>
    private static bool TryParseEnum<TEnum>(string? raw, TEnum defaultValue, out TEnum value, out string? error) where TEnum : struct, Enum
    {
        error = null;
        if (string.IsNullOrWhiteSpace(raw)) { value = defaultValue; return true; }
        if (Enum.TryParse<TEnum>(raw, ignoreCase: true, out var parsed)) { value = parsed; return true; }
        value = defaultValue;
        error = $"'{raw}' is not one of {string.Join(", ", Enum.GetNames<TEnum>())}";
        return false;
    }

    /// <summary>Mirrors BilingualEntity's [MinLength(3)] so the row is rejected
    /// before EF would throw on SaveChanges.</summary>
    private static bool ValidateName(string? name, string language, int rowNum, ImportResult result)
    {
        if (string.IsNullOrWhiteSpace(name))
        { result.Errors.Add(new RowError(rowNum, $"{language} name is required.")); return false; }
        if (name.Length < 3)
        { result.Errors.Add(new RowError(rowNum, $"{language} name must be at least 3 characters.")); return false; }
        return true;
    }
}
