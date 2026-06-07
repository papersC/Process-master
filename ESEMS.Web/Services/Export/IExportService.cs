using ESEMS.Web.Services.Common;

namespace ESEMS.Web.Services.Export;

public interface IExportService
{
    // F-025: the per-entity PDF methods (ExportProcesses/Services/RisksToPdfAsync)
    // were removed — they were never called (the live PDF export builds its
    // QuestPDF documents directly in Api/ExportController) and their shared
    // helper was broken (no ColumnsDefinition → throws at compose). Excel/CSV
    // exports below are the real, used surface.
    Task<byte[]> ExportProcessesToExcelAsync();
    Task<byte[]> ExportServicesToExcelAsync();
    Task<byte[]> ExportRisksToExcelAsync();
    Task<byte[]> ExportIncidentsToExcelAsync();
    Task<byte[]> ExportImprovementsToExcelAsync(ScopeContext scope);
    Task<byte[]> ExportToCsvAsync(string entityType);

    /// <summary>
    /// Audit #20: empty-template XLSX showing the column layout the import
    /// expects. Downloaded from the Improvements/Index toolbar.
    /// </summary>
    byte[] BuildImprovementsImportTemplate();

    /// <summary>
    /// Audit #20: parse an uploaded XLSX file produced from the template
    /// and insert/update <see cref="Models.Improvement.ImprovementInitiative"/>
    /// rows in bulk. Returns a per-row report so the UI can show the user
    /// which rows landed and which failed.
    /// </summary>
    Task<ImportImprovementsResult> ImportImprovementsFromExcelAsync(Stream xlsxStream, string? createdById, CancellationToken ct = default);
}

/// <summary>
/// Per-row outcome of <see cref="IExportService.ImportImprovementsFromExcelAsync"/>.
/// </summary>
public class ImportImprovementsResult
{
    public int TotalRows { get; set; }
    public int Inserted { get; set; }
    public int Skipped { get; set; }
    public List<ImportImprovementsRowError> Errors { get; set; } = new();
}

public class ImportImprovementsRowError
{
    public int Row { get; set; }
    public string Message { get; set; } = string.Empty;
}
