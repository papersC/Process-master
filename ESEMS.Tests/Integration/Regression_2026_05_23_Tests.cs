using System.Net;
using System.Text;
using System.Text.Json;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Import;
using ESEMS.Web.Models.RiskManagement;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ESEMS.Tests.Integration;

/// <summary>
/// HTTP-pipeline regression tests for the bigTest audit-fix campaign shipped
/// 2026-05-23 (commit f70f069). These drive the real middleware + controller
/// stack via the WebApplicationFactory.
///
///   F-012  SettingsHub/ImportUpload rejects non-spreadsheet uploads by
///          extension AND by magic bytes, with a friendly message.
///   F-024  RevertImport deletes imported EnterpriseRisks (the new branch that
///          makes the standard Risks template import undoable).
///
/// The per-record IDOR guards (F-001/F-003/F-020 and, by the same idiom,
/// F-006/F-007/F-008/F-019) are covered as deterministic controller-level
/// tests in Security/IdorScopeGuardTests; the standard-importer Created
/// tracking (F-024) at the service level in Services/ImportCreatedTrackingTests;
/// the Arabic-PDF font (F-002) in Services/PdfFontsTests.
/// </summary>
public class Regression_2026_05_23_Tests : IClassFixture<EsemsTestFactory>
{
    private readonly EsemsTestFactory _factory;
    public Regression_2026_05_23_Tests(EsemsTestFactory factory) { _factory = factory; }

    private HttpClient Admin()
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Role", "ADMIN");
        c.DefaultRequestHeaders.Add("X-Test-Perms", "*.*");
        c.DefaultRequestHeaders.Add("X-Test-UserId", "1");
        c.DefaultRequestHeaders.Add("X-Test-Name", "admin");
        return c;
    }

    // ─────────────────────────────────────────────────────────────────────
    // F-012 — server-side import file-type validation
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task F012_ImportUpload_RejectsWrongExtension()
    {
        using var form = new MultipartFormDataContent { { new StringContent("processes"), "kind" } };
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("definitely not a workbook")), "file", "evil.txt");

        var resp = await Admin().PostAsync("/SettingsHub/ImportUpload", form);
        var body = (await resp.Content.ReadAsStringAsync()).Replace(" ", "");

        Assert.Contains("\"success\":false", body);
        Assert.Contains("Unsupportedfiletype", body);
    }

    [Fact]
    public async Task F012_ImportUpload_RejectsBadMagicBytes()
    {
        // .xlsx extension but the bytes are not a ZIP/OLE2 container.
        using var form = new MultipartFormDataContent { { new StringContent("processes"), "kind" } };
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("this masquerades as xlsx but is not")), "file", "fake.xlsx");

        var resp = await Admin().PostAsync("/SettingsHub/ImportUpload", form);
        var body = (await resp.Content.ReadAsStringAsync()).Replace(" ", "");

        Assert.Contains("\"success\":false", body);
        Assert.Contains("notavalidExcelworkbook", body);
    }

    // ─────────────────────────────────────────────────────────────────────
    // F-024 — RevertImport undoes an imported Risks run (new EnterpriseRisks branch)
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task F024_RevertImport_DeletesImportedRisks()
    {
        var riskId = "risk-f024-" + Guid.NewGuid();
        var batchId = Guid.NewGuid().ToString();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.EnterpriseRisks.Add(new EnterpriseRisk
            {
                Id = riskId,
                RiskNumber = "R-F024",
                NameEn = "Reversible risk", NameAr = "خطر قابل للتراجع",
                CategoryId = "rc-f024"
            });
            // Manifest is the exact shape ImportUpload writes: [{ "T": table, "Id": id }].
            var manifest = JsonSerializer.Serialize(new[] { new { T = "EnterpriseRisks", Id = riskId } });
            db.ImportBatches.Add(new ImportBatch
            {
                Id = batchId, Kind = "risks", FileName = "risks.xlsx", ImportedCount = 1, Manifest = manifest
            });
            await db.SaveChangesAsync();
        }

        var resp = await Admin().PostAsync("/SettingsHub/RevertImport",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = batchId }));
        var body = (await resp.Content.ReadAsStringAsync()).Replace(" ", "");
        Assert.Contains("\"success\":true", body);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stillThere = await db.EnterpriseRisks.AsNoTracking().AnyAsync(r => r.Id == riskId);
            Assert.False(stillThere, "F-024: RevertImport must delete the imported EnterpriseRisk rows.");
        }
    }
}
