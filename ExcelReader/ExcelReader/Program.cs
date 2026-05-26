using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;

var filePath = @"C:\Users\kalmi\OneDrive\Desktop\MB1\ExcelReader\full_copy.xlsx";
Console.OutputEncoding = System.Text.Encoding.UTF8;

try
{
    using var doc = SpreadsheetDocument.Open(filePath, false);
    var workbookPart = doc.WorkbookPart!;
    var sheets = workbookPart.Workbook.Sheets!.Elements<Sheet>().ToList();
    var stringTable = workbookPart.SharedStringTablePart?.SharedStringTable;

    // Find the Process Cataloge sheet
    var processCatalogSheet = sheets.FirstOrDefault(s => s.Name?.Value?.Contains("Process Cataloge") == true);
    if (processCatalogSheet == null)
    {
        Console.WriteLine("Process Cataloge sheet not found!");
        return;
    }

    var wsPart = (WorksheetPart)workbookPart.GetPartById(processCatalogSheet.Id!.Value!);
    var sheetData = wsPart.Worksheet.Elements<SheetData>().FirstOrDefault();
    var rows = sheetData!.Elements<Row>().ToList();

    // Analysis containers
    var sectors = new HashSet<string>();
    var departments = new Dictionary<string, HashSet<string>>();
    var sections = new Dictionary<string, HashSet<string>>();
    var processGroups = new HashSet<string>();
    var mainProcesses = new HashSet<string>();
    var subProcesses = new HashSet<string>();
    var procedures = new HashSet<string>();
    var digitalSystems = new HashSet<string>();
    var automationStatuses = new Dictionary<string, int>();
    var procedureStatuses = new Dictionary<string, int>();
    var services = new HashSet<string>();
    var partners = new HashSet<string>();
    var projects = new HashSet<string>();
    var processRecords = new List<Dictionary<string, string>>();

    // Detailed procedure records for Level 5 import
    var procedureDetails = new List<object>();

    // Column mapping (based on header row analysis)
    var columnMap = new Dictionary<string, int>
    {
        {"RowNum", 1}, {"StrategicObjectives", 2}, {"MandateResponsibilities", 3},
        {"SectorAr", 4}, {"SectorEn", 5}, {"DepartmentAr", 6}, {"DepartmentEn", 7},
        {"SectionAr", 8}, {"SectionEn", 9}, {"Committee", 10}, {"Tasks", 11},
        {"ProcessGroup", 12}, {"ProcessClassification", 13}, {"MainProcessAr", 14},
        {"MainProcessEn", 15}, {"SubProcessAr", 16}, {"SubProcessEn", 17},
        {"SubProcessDesc", 18}, {"ProcedureAr", 19}, {"ProcedureEn", 20},
        {"ProcedureDesc", 21}, {"ProcedureStatus", 22}, {"CurrentProposed", 23},
        {"AutomationStatus", 24}, {"DigitalSystem", 25}, {"Automable", 26},
        {"AutoScore1", 27}, {"AutoScore2", 28}, {"AutoScore3", 29}, {"AutoScore4", 30},
        {"AutoScore5", 31}, {"ServiceLink", 32}, {"ServiceNames", 33},
        {"ExternalPartners", 34}, {"Projects", 35}, {"GovPartners", 36},
        {"DocRef", 37}, {"DocLang", 38}, {"DocCode", 39}
    };

    Console.WriteLine("=== PROCESS CATALOG ANALYSIS ===\n");
    Console.WriteLine($"Total Rows: {rows.Count - 1} (excluding header)\n");

    // Process each data row (skip header rows 1-2)
    foreach (var row in rows.Skip(2))
    {
        var record = new Dictionary<string, string>();
        var cells = row.Elements<Cell>().ToList();

        foreach (var cell in cells)
        {
            var colRef = Regex.Replace(cell.CellReference?.Value ?? "", @"\d+", "");
            var value = GetCellValue(cell, stringTable);
            record[colRef] = value;
        }

        // Extract key fields
        var sectorEn = record.GetValueOrDefault("E", "").Trim();
        var sectorAr = record.GetValueOrDefault("D", "").Trim();
        var deptEn = record.GetValueOrDefault("G", "").Trim();
        var deptAr = record.GetValueOrDefault("F", "").Trim();
        var sectionEn = record.GetValueOrDefault("I", "").Trim();
        var sectionAr = record.GetValueOrDefault("H", "").Trim();
        var processGroup = record.GetValueOrDefault("L", "").Trim();
        var mainProcessEn = record.GetValueOrDefault("O", "").Trim();
        var mainProcessAr = record.GetValueOrDefault("N", "").Trim();
        var subProcessEn = record.GetValueOrDefault("Q", "").Trim();
        var subProcessAr = record.GetValueOrDefault("P", "").Trim();
        var procedureEn = record.GetValueOrDefault("T", "").Trim();
        var procedureAr = record.GetValueOrDefault("S", "").Trim();
        var procedureStatus = record.GetValueOrDefault("V", "").Trim();
        var automationStatus = record.GetValueOrDefault("X", "").Trim();
        var digitalSystem = record.GetValueOrDefault("Y", "").Trim();
        var serviceNames = record.GetValueOrDefault("AG", "").Trim();
        var govPartners = record.GetValueOrDefault("AJ", "").Trim();
        var projectsInit = record.GetValueOrDefault("AI", "").Trim();

        // Collect unique values
        if (!string.IsNullOrEmpty(sectorEn)) sectors.Add($"{sectorEn}|{sectorAr}");

        if (!string.IsNullOrEmpty(deptEn))
        {
            if (!departments.ContainsKey(sectorEn)) departments[sectorEn] = new HashSet<string>();
            departments[sectorEn].Add($"{deptEn}|{deptAr}");
        }

        if (!string.IsNullOrEmpty(sectionEn))
        {
            if (!sections.ContainsKey(deptEn)) sections[deptEn] = new HashSet<string>();
            sections[deptEn].Add($"{sectionEn}|{sectionAr}");
        }

        if (!string.IsNullOrEmpty(processGroup)) processGroups.Add(processGroup);
        if (!string.IsNullOrEmpty(mainProcessEn)) mainProcesses.Add($"{mainProcessEn}|{mainProcessAr}");
        if (!string.IsNullOrEmpty(subProcessEn)) subProcesses.Add($"{subProcessEn}|{subProcessAr}");
        if (!string.IsNullOrEmpty(procedureEn)) procedures.Add($"{procedureEn}|{procedureAr}");

        // Collect detailed procedure record for Level 5 import
        if (!string.IsNullOrEmpty(procedureEn))
        {
            var procedureDesc = record.GetValueOrDefault("U", "").Trim();
            var currentProposed = record.GetValueOrDefault("W", "").Trim();
            var automable = record.GetValueOrDefault("Z", "").Trim();
            var autoScore1 = record.GetValueOrDefault("AA", "").Trim();
            var autoScore2 = record.GetValueOrDefault("AB", "").Trim();
            var autoScore3 = record.GetValueOrDefault("AC", "").Trim();
            var autoScore4 = record.GetValueOrDefault("AD", "").Trim();
            var autoScore5 = record.GetValueOrDefault("AE", "").Trim();
            var docRef = record.GetValueOrDefault("AK", "").Trim();
            var docLang = record.GetValueOrDefault("AL", "").Trim();

            procedureDetails.Add(new
            {
                NameEn = procedureEn,
                NameAr = procedureAr,
                DescriptionEn = procedureDesc,
                SubProcessEn = subProcessEn,
                SubProcessAr = subProcessAr,
                MainProcessEn = mainProcessEn,
                MainProcessAr = mainProcessAr,
                SectionEn = sectionEn,
                SectionAr = sectionAr,
                DepartmentEn = deptEn,
                DepartmentAr = deptAr,
                SectorEn = sectorEn,
                SectorAr = sectorAr,
                ProcessGroup = processGroup,
                ProcedureStatus = procedureStatus,
                CurrentProposed = currentProposed,
                AutomationStatus = automationStatus,
                DigitalSystem = digitalSystem,
                Automable = automable,
                AutomationScores = $"{autoScore1}|{autoScore2}|{autoScore3}|{autoScore4}|{autoScore5}",
                LinkedServices = serviceNames,
                ExternalPartners = govPartners,
                Projects = projectsInit,
                DocumentReference = docRef,
                DocumentLanguage = docLang
            });
        }

        if (!string.IsNullOrEmpty(digitalSystem))
        {
            foreach (var sys in digitalSystem.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                digitalSystems.Add(sys.Trim());
        }

        if (!string.IsNullOrEmpty(automationStatus))
        {
            automationStatuses.TryGetValue(automationStatus, out int count);
            automationStatuses[automationStatus] = count + 1;
        }

        if (!string.IsNullOrEmpty(procedureStatus))
        {
            procedureStatuses.TryGetValue(procedureStatus, out int count);
            procedureStatuses[procedureStatus] = count + 1;
        }

        if (!string.IsNullOrEmpty(serviceNames))
        {
            foreach (var svc in serviceNames.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                services.Add(svc.Trim());
        }

        if (!string.IsNullOrEmpty(govPartners))
        {
            foreach (var partner in govPartners.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                if (partner.Trim() != "N/A") partners.Add(partner.Trim());
        }

        if (!string.IsNullOrEmpty(projectsInit))
        {
            foreach (var proj in projectsInit.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                projects.Add(proj.Trim());
        }

        processRecords.Add(record);
    }

    // Output Analysis Results
    Console.WriteLine("=== ORGANIZATIONAL STRUCTURE ===\n");
    Console.WriteLine($"Sectors: {sectors.Count}");
    foreach (var s in sectors) Console.WriteLine($"  - {s}");

    Console.WriteLine($"\nDepartments by Sector:");
    foreach (var kvp in departments)
    {
        Console.WriteLine($"  {kvp.Key}: {kvp.Value.Count} departments");
        foreach (var d in kvp.Value) Console.WriteLine($"    - {d}");
    }

    Console.WriteLine($"\nSections by Department:");
    foreach (var kvp in sections)
    {
        Console.WriteLine($"  {kvp.Key}: {kvp.Value.Count} sections");
        foreach (var s in kvp.Value) Console.WriteLine($"    - {s}");
    }

    Console.WriteLine("\n=== PROCESS HIERARCHY ===\n");
    Console.WriteLine($"Process Groups: {processGroups.Count}");
    foreach (var pg in processGroups) Console.WriteLine($"  - {pg}");

    Console.WriteLine($"\nMain Processes: {mainProcesses.Count}");
    foreach (var mp in mainProcesses.Take(20)) Console.WriteLine($"  - {mp}");
    if (mainProcesses.Count > 20) Console.WriteLine($"  ... and {mainProcesses.Count - 20} more");

    Console.WriteLine($"\nSub-Processes: {subProcesses.Count}");
    Console.WriteLine($"Procedures: {procedures.Count}");

    Console.WriteLine("\n=== AUTOMATION & DIGITAL SYSTEMS ===\n");
    Console.WriteLine("Automation Status Distribution:");
    foreach (var kvp in automationStatuses.OrderByDescending(x => x.Value))
        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");

    Console.WriteLine($"\nDigital Systems: {digitalSystems.Count}");
    foreach (var ds in digitalSystems) Console.WriteLine($"  - {ds}");

    Console.WriteLine("\n=== PROCEDURE STATUS ===\n");
    foreach (var kvp in procedureStatuses.OrderByDescending(x => x.Value))
        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");

    Console.WriteLine("\n=== SERVICES ===\n");
    Console.WriteLine($"Linked Services: {services.Count}");
    foreach (var svc in services.Take(30)) Console.WriteLine($"  - {svc}");
    if (services.Count > 30) Console.WriteLine($"  ... and {services.Count - 30} more");

    Console.WriteLine("\n=== EXTERNAL PARTNERS ===\n");
    Console.WriteLine($"Government Partners: {partners.Count}");
    foreach (var p in partners) Console.WriteLine($"  - {p}");

    Console.WriteLine("\n=== PROJECTS & INITIATIVES ===\n");
    Console.WriteLine($"Projects: {projects.Count}");
    foreach (var p in projects) Console.WriteLine($"  - {p}");

    // Export to JSON for import
    var exportData = new
    {
        Sectors = sectors.Select(s => { var parts = s.Split('|'); return new { NameEn = parts[0], NameAr = parts.Length > 1 ? parts[1] : "" }; }).ToList(),
        Departments = departments.SelectMany(kvp => kvp.Value.Select(d => { var parts = d.Split('|'); return new { Sector = kvp.Key, NameEn = parts[0], NameAr = parts.Length > 1 ? parts[1] : "" }; })).ToList(),
        Sections = sections.SelectMany(kvp => kvp.Value.Select(s => { var parts = s.Split('|'); return new { Department = kvp.Key, NameEn = parts[0], NameAr = parts.Length > 1 ? parts[1] : "" }; })).ToList(),
        ProcessGroups = processGroups.ToList(),
        MainProcesses = mainProcesses.Select(mp => { var parts = mp.Split('|'); return new { NameEn = parts[0], NameAr = parts.Length > 1 ? parts[1] : "" }; }).ToList(),
        SubProcesses = subProcesses.Select(sp => { var parts = sp.Split('|'); return new { NameEn = parts[0], NameAr = parts.Length > 1 ? parts[1] : "" }; }).ToList(),
        Procedures = procedures.Select(p => { var parts = p.Split('|'); return new { NameEn = parts[0], NameAr = parts.Length > 1 ? parts[1] : "" }; }).ToList(),
        ProcedureDetails = procedureDetails, // Detailed procedure records for Level 5 import
        DigitalSystems = digitalSystems.ToList(),
        Services = services.ToList(),
        Partners = partners.ToList(),
        Projects = projects.ToList(),
        AutomationStatuses = automationStatuses,
        ProcedureStatuses = procedureStatuses,
        TotalRecords = processRecords.Count,
        TotalProcedures = procedureDetails.Count
    };

    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };
    var json = JsonSerializer.Serialize(exportData, jsonOptions);
    File.WriteAllText(@"C:\Users\kalmi\OneDrive\Desktop\MB1\ExcelReader\analysis_output.json", json);
    Console.WriteLine("\n=== JSON EXPORT COMPLETE ===");
    Console.WriteLine(@"Saved to: C:\Users\kalmi\OneDrive\Desktop\MB1\ExcelReader\analysis_output.json");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

static string GetCellValue(Cell cell, SharedStringTable? stringTable)
{
    if (cell.CellValue == null) return "";
    var value = cell.CellValue.InnerText;

    if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString && stringTable != null)
    {
        if (int.TryParse(value, out int index))
            return stringTable.ElementAt(index).InnerText;
    }
    return value;
}
