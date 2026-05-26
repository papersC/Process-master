using ClosedXML.Excel;
using ESEMS.Tests.TestFixtures;
using ESEMS.Web.Models.RiskManagement;
using ESEMS.Web.Services.Import;
using Microsoft.Extensions.Logging.Abstractions;

namespace ESEMS.Tests.Services;

/// <summary>
/// F-024: the four standard template importers (Process/Service/Asset/Risk)
/// previously did AddRange + SaveChanges with no manifest, so a run couldn't be
/// undone from "Recent imports". They now populate <c>ImportResult.Created</c>
/// with (logical table name, row id) — and the table name must match what
/// SettingsHubController.RevertImport switches on. This proves the Risks
/// importer (the one whose RevertImport branch was newly added) records its
/// rows under the "EnterpriseRisks" tag.
/// </summary>
public class ImportCreatedTrackingTests
{
    private static MemoryStream BuildRisksXlsx(string categoryCode)
    {
        // Header row must match ExcelImportService.RiskHeaders exactly.
        var headers = new[]
        {
            "Risk Number", "Name (English)", "Name (Arabic)", "Description (English)",
            "Description (Arabic)", "Category Code", "Likelihood (1-5)", "Impact (1-5)"
        };

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Risks");
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Cell(2, 1).Value = "RSK-T1";
        ws.Cell(2, 2).Value = "Test risk";
        ws.Cell(2, 3).Value = "خطر تجريبي";
        ws.Cell(2, 4).Value = "desc";
        ws.Cell(2, 5).Value = "وصف";
        ws.Cell(2, 6).Value = categoryCode;
        ws.Cell(2, 7).Value = 3;
        ws.Cell(2, 8).Value = 3;

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task ImportRisksAsync_PopulatesCreated_TaggedEnterpriseRisks()
    {
        using var db = TestDbContextFactory.Create();
        db.RiskCategories.Add(new RiskCategory { Id = "rc-t", Code = "RC-T", NameEn = "Cat", NameAr = "فئة" });
        await db.SaveChangesAsync();

        var importer = new ExcelImportService(db, NullLogger<ExcelImportService>.Instance);
        using var stream = BuildRisksXlsx("RC-T");
        var result = await importer.ImportRisksAsync(stream);

        Assert.Null(result.FatalError);
        Assert.Empty(result.Errors);
        Assert.Equal(1, result.Imported);

        // The manifest entry RevertImport needs: table name "EnterpriseRisks".
        Assert.Contains(result.Created, c => c.Table == "EnterpriseRisks");
        Assert.Single(result.Created);
    }
}
