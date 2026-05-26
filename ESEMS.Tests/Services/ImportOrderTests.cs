using ClosedXML.Excel;
using ESEMS.Tests.TestFixtures;
using ESEMS.Web.Models.AssetManagement;
using ESEMS.Web.Services.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ESEMS.Tests.Services;

/// <summary>
/// F-023: the MBRHE asset-register importer enforces import order. When the org
/// table is empty and the file names specific owner departments, the user
/// imported Assets before the Org Structure — every asset would land unassigned.
/// The importer fails the whole run (nothing saved) with a clear instruction
/// instead of silently creating a pile of unlinked assets.
/// </summary>
public class ImportOrderTests
{
    private static MemoryStream BuildAssetXlsx(string owner)
    {
        // Header row must match ExcelImportService.MbrheAssetHeaders exactly.
        var headers = new[] { "Asset Name", "Description", "Classification", "Owner / Department" };
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Assets");
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Cell(2, 1).Value = "أصل تجريبي";
        ws.Cell(2, 2).Value = "desc";
        ws.Cell(2, 3).Value = "Physical";
        ws.Cell(2, 4).Value = owner;

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static void SeedAssetCategories(ESEMS.Web.Data.ApplicationDbContext db)
    {
        // The importer requires these two seed categories to exist.
        db.AssetCategories.Add(new AssetCategory { Id = "re", Code = "AST-RE-PROJ", NameEn = "Real estate", NameAr = "عقار" });
        db.AssetCategories.Add(new AssetCategory { Id = "info", Code = "AST-INFO-APP", NameEn = "Info asset", NameAr = "معلومات" });
    }

    [Fact]
    public async Task AssetImport_NoOrgUnits_SpecificOwner_FailsFast_NothingSaved()
    {
        using var db = TestDbContextFactory.Create();
        SeedAssetCategories(db);
        await db.SaveChangesAsync(); // NOTE: no OrganizationUnits seeded

        var importer = new ExcelImportService(db, NullLogger<ExcelImportService>.Instance);
        using var stream = BuildAssetXlsx("إدارة الإسكان"); // a specific (non-MBRHE) owner
        var result = await importer.ImportMbrheAssetRegisterAsync(stream);

        Assert.NotNull(result.FatalError);
        Assert.Contains("Org Structure", result.FatalError);
        Assert.Equal(0, result.Imported);
        Assert.False(await db.Assets.AnyAsync(), "nothing should be persisted on a fail-fast");
    }

    [Fact]
    public async Task AssetImport_MbrheRootOwner_NoOrgUnits_StillImports()
    {
        // "MBRHE" maps to the corporate root (no specific unit), so an empty org
        // table is NOT the wrong-order mistake — the asset imports unassigned.
        using var db = TestDbContextFactory.Create();
        SeedAssetCategories(db);
        await db.SaveChangesAsync();

        var importer = new ExcelImportService(db, NullLogger<ExcelImportService>.Instance);
        using var stream = BuildAssetXlsx("MBRHE");
        var result = await importer.ImportMbrheAssetRegisterAsync(stream);

        Assert.Null(result.FatalError);
        Assert.Equal(1, result.Imported);
    }
}
