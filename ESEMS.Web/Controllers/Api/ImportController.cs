using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Services.AI;
using ESEMS.Web.Services.Analysis;
using ESEMS.Web.Services.Bpmn;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;

namespace ESEMS.Web.Controllers.Api;

/// <summary>
/// Controller for importing data from external sources (Excel analysis output)
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = ESEMS.Web.Security.AppPolicies.CanAdmin)]
public class ImportController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ImportController> _logger;
    private readonly IAIService _aiService;
    private readonly AWSBedrockService _bedrockService;
    private readonly IBpmnValidator _bpmnValidator;
    private readonly IBpmnPostProcessor _bpmnPostProcessor;
    private readonly IBpmnLaneReconciler _bpmnLaneReconciler;
    private readonly IVisioExtractor _visioExtractor;
    private readonly IProcessAnalysisService _analysisService;

    public ImportController(
        ApplicationDbContext context,
        ILogger<ImportController> logger,
        IAIService aiService,
        AWSBedrockService bedrockService,
        IBpmnValidator bpmnValidator,
        IBpmnPostProcessor bpmnPostProcessor,
        IBpmnLaneReconciler bpmnLaneReconciler,
        IVisioExtractor visioExtractor,
        IProcessAnalysisService analysisService)
    {
        _context = context;
        _logger = logger;
        _aiService = aiService;
        _bedrockService = bedrockService;
        _bpmnValidator = bpmnValidator;
        _bpmnPostProcessor = bpmnPostProcessor;
        _bpmnLaneReconciler = bpmnLaneReconciler;
        _visioExtractor = visioExtractor;
        _analysisService = analysisService;
    }

    // Track codes used in current import session to avoid duplicates
    private readonly HashSet<string> _usedCodes = new();

    /// <summary>
    /// Import organizational structure from analysis JSON
    /// </summary>
    [HttpPost("organization")]
    public async Task<IActionResult> ImportOrganization([FromBody] OrganizationImportData data)
    {
        try
        {
            var createdUnits = new List<OrganizationUnit>();
            // Id is a DB identity (int) now and isn't known until SaveChanges, so
            // map English name -> the unit OBJECT and wire children via the Parent
            // navigation. Each level is saved before the next, so EF resolves the
            // parent FK on save.
            var sectorMap = new Dictionary<string, OrganizationUnit>();
            var deptMap = new Dictionary<string, OrganizationUnit>();

            // Get all existing organization units to avoid duplicates
            var existingUnits = await _context.OrganizationUnits.ToListAsync();

            // Pre-populate used codes with existing database codes
            _usedCodes.Clear();
            foreach (var unit in existingUnits)
            {
                _usedCodes.Add(unit.Code);
            }

            // 1. Import Sectors (Level 0)
            foreach (var sector in data.Sectors)
            {
                var existing = existingUnits.FirstOrDefault(o => o.NameEn == sector.NameEn && o.Level == 0);

                if (existing == null)
                {
                    var code = GenerateUniqueCode("SEC", sector.NameEn);
                    var unit = new OrganizationUnit
                    {
                        NameEn = sector.NameEn,
                        NameAr = sector.NameAr,
                        Code = code,
                        Level = 0,
                        IsActive = true,
                        DisplayOrder = createdUnits.Count(u => u.Level == 0) + 1
                    };
                    _context.OrganizationUnits.Add(unit);
                    createdUnits.Add(unit);
                    sectorMap[sector.NameEn] = unit;
                }
                else
                {
                    sectorMap[sector.NameEn] = existing;
                }
            }
            await _context.SaveChangesAsync();

            // 2. Import Departments (Level 1)
            foreach (var dept in data.Departments)
            {
                if (string.IsNullOrEmpty(dept.NameEn)) continue;

                var parent = sectorMap.GetValueOrDefault(dept.Sector);
                var existing = existingUnits.FirstOrDefault(o => o.NameEn == dept.NameEn && o.Level == 1);

                if (existing == null)
                {
                    var code = GenerateUniqueCode("DEP", dept.NameEn);
                    var unit = new OrganizationUnit
                    {
                        NameEn = dept.NameEn,
                        NameAr = dept.NameAr,
                        Code = code,
                        Level = 1,
                        Parent = parent,
                        IsActive = true,
                        DisplayOrder = createdUnits.Count(u => u.Level == 1) + 1
                    };
                    _context.OrganizationUnits.Add(unit);
                    createdUnits.Add(unit);
                    deptMap[dept.NameEn] = unit;
                }
                else
                {
                    deptMap[dept.NameEn] = existing;
                }
            }
            await _context.SaveChangesAsync();

            // 3. Import Sections (Level 2)
            foreach (var section in data.Sections)
            {
                if (string.IsNullOrEmpty(section.NameEn)) continue;

                var parent = deptMap.GetValueOrDefault(section.Department);
                var existing = existingUnits.FirstOrDefault(o => o.NameEn == section.NameEn && o.Level == 2);

                if (existing == null)
                {
                    var code = GenerateUniqueCode("SCT", section.NameEn);
                    var unit = new OrganizationUnit
                    {
                        NameEn = section.NameEn,
                        NameAr = section.NameAr,
                        Code = code,
                        Level = 2,
                        Parent = parent,
                        IsActive = true,
                        DisplayOrder = createdUnits.Count(u => u.Level == 2) + 1
                    };
                    _context.OrganizationUnits.Add(unit);
                    createdUnits.Add(unit);
                }
            }
            await _context.SaveChangesAsync();

            return Ok(new { 
                success = true, 
                message = $"Imported {createdUnits.Count} organization units",
                sectors = createdUnits.Count(u => u.Level == 0),
                departments = createdUnits.Count(u => u.Level == 1),
                sections = createdUnits.Count(u => u.Level == 2)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing organization structure");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Import processes from analysis JSON
    /// </summary>
    [HttpPost("processes")]
    public async Task<IActionResult> ImportProcesses([FromBody] ProcessImportData data)
    {
        try
        {
            var createdProcesses = new List<Process>();
            var processGroupMap = new Dictionary<string, string>();
            var mainProcessMap = new Dictionary<string, string>();

            // 1. Create/Get default Category (APQC Level 1)
            var defaultCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Code == "MBRHE");
            if (defaultCategory == null)
            {
                defaultCategory = new Category
                {
                    Id = Guid.NewGuid().ToString(),
                    Code = "MBRHE",
                    NameEn = "MBRHE Process Catalog",
                    NameAr = "دليل عمليات مؤسسة محمد بن راشد للإسكان"
                };
                _context.Categories.Add(defaultCategory);
                await _context.SaveChangesAsync();
            }

            // 2. Import Process Groups (APQC Level 2)
            var pgIndex = 0;
            foreach (var pg in data.ProcessGroups)
            {
                if (string.IsNullOrEmpty(pg)) continue;

                var existing = await _context.ProcessGroups
                    .FirstOrDefaultAsync(p => p.NameAr == pg || p.NameEn == pg);

                if (existing == null)
                {
                    var group = new ProcessGroup
                    {
                        Id = Guid.NewGuid().ToString(),
                        NameAr = pg,
                        NameEn = pg,
                        Code = $"PG-{pgIndex + 1:D2}",
                        CategoryId = defaultCategory.Id
                    };
                    _context.ProcessGroups.Add(group);
                    processGroupMap[pg] = group.Id;
                    pgIndex++;
                }
                else
                {
                    processGroupMap[pg] = existing.Id;
                }
            }
            await _context.SaveChangesAsync();

            // Track main vs sub processes separately
            var mainProcessCount = 0;
            var subProcessCount = 0;

            // 3. Import Main Processes (APQC Level 3 - using ClassificationType.Main)
            var defaultGroupId = processGroupMap.Values.FirstOrDefault();
            foreach (var mp in data.MainProcesses)
            {
                if (string.IsNullOrEmpty(mp.NameEn)) continue;

                var existing = await _context.Processes
                    .FirstOrDefaultAsync(p => p.NameEn == mp.NameEn && p.ClassificationType == ProcessClassificationType.Main);

                if (existing == null)
                {
                    mainProcessCount++;
                    var process = new Process
                    {
                        Id = Guid.NewGuid().ToString(),
                        NameEn = mp.NameEn,
                        NameAr = mp.NameAr,
                        Code = $"MP-{mainProcessCount:D3}",
                        ProcessGroupId = defaultGroupId ?? string.Empty,
                        ClassificationType = ProcessClassificationType.Main,
                        DisplayOrder = mainProcessCount
                    };
                    _context.Processes.Add(process);
                    createdProcesses.Add(process);
                    mainProcessMap[mp.NameEn] = process.Id;
                }
                else
                {
                    mainProcessMap[mp.NameEn] = existing.Id;
                }
            }
            await _context.SaveChangesAsync();

            // 4. Import Sub-Processes (APQC Level 3 with ClassificationType.Support)
            foreach (var sp in data.SubProcesses)
            {
                if (string.IsNullOrEmpty(sp.NameEn)) continue;

                var existing = await _context.Processes
                    .FirstOrDefaultAsync(p => p.NameEn == sp.NameEn && p.ClassificationType == ProcessClassificationType.Support);

                if (existing == null)
                {
                    subProcessCount++;
                    var process = new Process
                    {
                        Id = Guid.NewGuid().ToString(),
                        NameEn = sp.NameEn,
                        NameAr = sp.NameAr,
                        Code = $"SP-{subProcessCount:D3}",
                        ProcessGroupId = defaultGroupId ?? string.Empty,
                        ClassificationType = ProcessClassificationType.Support,
                        DisplayOrder = mainProcessCount + subProcessCount
                    };
                    _context.Processes.Add(process);
                    createdProcesses.Add(process);
                }
            }
            await _context.SaveChangesAsync();

            return Ok(new {
                success = true,
                message = $"Imported {createdProcesses.Count} processes",
                processGroups = data.ProcessGroups.Count,
                mainProcesses = mainProcessCount,
                subProcesses = subProcessCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing processes");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Import all data from the analysis JSON file
    /// </summary>
    [HttpPost("all")]
    public async Task<IActionResult> ImportAll()
    {
        try
        {
            var jsonPath = @"C:\Users\kalmi\OneDrive\Desktop\MB1\ExcelReader\analysis_output.json";
            if (!System.IO.File.Exists(jsonPath))
            {
                return BadRequest(new { success = false, message = "Analysis file not found" });
            }

            var jsonContent = await System.IO.File.ReadAllTextAsync(jsonPath);
            var analysisData = JsonSerializer.Deserialize<AnalysisOutput>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (analysisData == null)
            {
                return BadRequest(new { success = false, message = "Failed to parse analysis file" });
            }

            // Import organization structure
            var orgResult = await ImportOrganization(new OrganizationImportData
            {
                Sectors = analysisData.Sectors,
                Departments = analysisData.Departments,
                Sections = analysisData.Sections
            });

            // Import processes
            var processResult = await ImportProcesses(new ProcessImportData
            {
                ProcessGroups = analysisData.ProcessGroups,
                MainProcesses = analysisData.MainProcesses,
                SubProcesses = analysisData.SubProcesses,
                Procedures = analysisData.Procedures
            });

            // Import procedures (Level 5 - ProcessTasks)
            var procedureResult = await ImportProcedures(analysisData.ProcedureDetails);

            // Import Services
            var servicesResult = await ImportServicesInternal(analysisData.Services);

            // Import Digital Systems
            var systemsResult = await ImportDigitalSystemsInternal(analysisData.DigitalSystems);

            // Import Partners
            var partnersResult = await ImportPartnersInternal(analysisData.Partners);

            // Import Projects
            var projectsResult = await ImportProjectsInternal(analysisData.Projects);

            return Ok(new {
                success = true,
                message = "Import completed successfully",
                totalRecords = analysisData.TotalRecords,
                totalProcedures = analysisData.TotalProcedures,
                services = servicesResult,
                digitalSystems = systemsResult,
                partners = partnersResult,
                projects = projectsResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing all data");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Import procedures from analysis JSON as ProcessTasks (Level 5)
    /// </summary>
    [HttpPost("procedures")]
    public async Task<IActionResult> ImportProcedures([FromBody] List<ProcedureDetailItem> procedures)
    {
        try
        {
            int importedCount = 0;
            int skippedCount = 0;
            int noParentCount = 0;
            var activityMap = new Dictionary<string, string>(); // SubProcess name -> Activity ID

            // Pre-load all existing process tasks for faster lookup
            var existingTasks = await _context.ProcessTasks.Select(t => t.NameEn).ToListAsync();

            foreach (var proc in procedures)
            {
                if (string.IsNullOrEmpty(proc.NameEn)) continue;

                // Check if procedure already exists
                if (existingTasks.Contains(proc.NameEn))
                {
                    skippedCount++;
                    continue;
                }

                // Find or create parent activity based on sub-process
                string? activityId = null;
                if (!string.IsNullOrEmpty(proc.SubProcessEn))
                {
                    if (!activityMap.ContainsKey(proc.SubProcessEn))
                    {
                        // Find the parent process (try SubProcess name first, then try matching with NameEn/NameAr)
                        var parentProcess = await _context.Processes
                            .FirstOrDefaultAsync(p => p.NameEn == proc.SubProcessEn || p.NameAr == proc.SubProcessAr);

                        if (parentProcess != null)
                        {
                            // Check if activity already exists for this process
                            var existingActivity = await _context.Activities
                                .FirstOrDefaultAsync(a => a.ProcessId == parentProcess.Id);

                            if (existingActivity != null)
                            {
                                activityMap[proc.SubProcessEn] = existingActivity.Id;
                                activityId = existingActivity.Id;
                            }
                            else
                            {
                                // Create a placeholder activity for this sub-process
                                var activity = new Activity
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    ProcessId = parentProcess.Id,
                                    NameEn = $"Activities for {proc.SubProcessEn}",
                                    NameAr = $"أنشطة {proc.SubProcessAr}",
                                    Code = $"ACT-{parentProcess.Code}",
                                    DisplayOrder = 1
                                };
                                _context.Activities.Add(activity);
                                await _context.SaveChangesAsync();
                                activityMap[proc.SubProcessEn] = activity.Id;
                                activityId = activity.Id;
                            }
                        }
                        else
                        {
                            // No parent process found - log and skip
                            _logger.LogWarning($"No parent process found for procedure: {proc.NameEn} (SubProcess: {proc.SubProcessEn})");
                            noParentCount++;
                            continue;
                        }
                    }
                    else
                    {
                        activityId = activityMap[proc.SubProcessEn];
                    }
                }
                else
                {
                    // No sub-process specified - skip
                    _logger.LogWarning($"No sub-process specified for procedure: {proc.NameEn}");
                    noParentCount++;
                    continue;
                }

                // Parse automation status
                var automationStatus = proc.AutomationStatus switch
                {
                    "مؤتمتة" => AutomationStatus.Automated,
                    "شبه مؤتمتة" => AutomationStatus.SemiAutomated,
                    _ => AutomationStatus.Traditional
                };

                // Parse procedure status
                var procedureStatus = proc.ProcedureStatus switch
                {
                    "معتمد" => ProcedureStatus.Approved,
                    "ملغي" => ProcedureStatus.Cancelled,
                    _ => ProcedureStatus.Draft
                };

                // Parse automability status
                var automabilityStatus = proc.Automable switch
                {
                    "قابل للأتمتة" => AutomabilityStatus.Automatable,
                    "لا ينطبق" => AutomabilityStatus.NotAutomatable,
                    _ => AutomabilityStatus.PartiallyAutomatable
                };

                // Parse current/proposed
                var currentProposed = proc.CurrentProposed switch
                {
                    "إجراء حالي" => CurrentProposedStatus.Current,
                    "إجراء مقترح" => CurrentProposedStatus.Proposed,
                    _ => CurrentProposedStatus.Current
                };

                // Find owning unit by section
                int? owningUnitId = null;
                if (!string.IsNullOrEmpty(proc.SectionEn))
                {
                    var section = await _context.OrganizationUnits
                        .FirstOrDefaultAsync(o => o.NameEn == proc.SectionEn);
                    owningUnitId = section?.Id;
                }

                // Create the ProcessTask (procedure)
                var task = new ProcessTask
                {
                    Id = Guid.NewGuid().ToString(),
                    NameEn = proc.NameEn,
                    NameAr = proc.NameAr,
                    DescriptionEn = proc.DescriptionEn,
                    Code = $"PROC-{importedCount + 1:D3}",
                    ActivityId = activityId ?? string.Empty,
                    OwningUnitId = owningUnitId,
                    DisplayOrder = importedCount + 1,
                    AutomationStatus = automationStatus,
                    ProcedureStatus = procedureStatus,
                    AutomabilityStatus = automabilityStatus,
                    CurrentProposedStatus = currentProposed,
                    DigitalSystemName = proc.DigitalSystem,
                    AutomationAssessmentScores = proc.AutomationScores,
                    LinkedServices = proc.LinkedServices?.Replace("\n", ", "),
                    DocumentReference = proc.DocumentReference,
                    DocumentLanguage = proc.DocumentLanguage
                };

                _context.ProcessTasks.Add(task);
                importedCount++;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Imported {importedCount} procedures, skipped {skippedCount}, no parent: {noParentCount}",
                imported = importedCount,
                skipped = skippedCount,
                noParent = noParentCount,
                activities = activityMap.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing procedures");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Generate a unique code using in-memory tracking to avoid duplicates within same import session
    /// </summary>
    private string GenerateUniqueCode(string prefix, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            var fallbackCode = $"{prefix}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
            _usedCodes.Add(fallbackCode);
            return fallbackCode;
        }

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = string.Join("", words.Take(3).Select(w => w.Length > 0 ? w[0].ToString().ToUpper() : ""));
        var baseCode = $"{prefix}-{initials}";

        // Check if this code already exists in our tracking set
        if (!_usedCodes.Contains(baseCode))
        {
            _usedCodes.Add(baseCode);
            return baseCode;
        }

        // Add a numeric suffix to make it unique
        for (int i = 2; i <= 99; i++)
        {
            var candidateCode = $"{baseCode}{i}";
            if (!_usedCodes.Contains(candidateCode))
            {
                _usedCodes.Add(candidateCode);
                return candidateCode;
            }
        }

        // Fallback to GUID suffix
        var guidCode = $"{baseCode}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
        _usedCodes.Add(guidCode);
        return guidCode;
    }

    /// <summary>
    /// Import services from analysis data
    /// </summary>
    private async Task<int> ImportServicesInternal(List<string> services)
    {
        if (services == null || services.Count == 0) return 0;

        int importedCount = 0;
        var existingServices = await _context.Services.Select(s => s.NameAr).ToListAsync();

        foreach (var serviceName in services)
        {
            if (string.IsNullOrEmpty(serviceName) || serviceName == "--" || serviceName.StartsWith(")")) continue;

            if (existingServices.Contains(serviceName)) continue;

            var service = new Service
            {
                Id = Guid.NewGuid().ToString(),
                NameAr = serviceName,
                NameEn = serviceName, // Use Arabic as English placeholder
                Code = GenerateUniqueCode("SVC", serviceName),
                ServiceType = ServiceType.External,
                IsActive = true,
                DisplayOrder = importedCount + 1
            };

            _context.Services.Add(service);
            existingServices.Add(serviceName);
            importedCount++;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation($"Imported {importedCount} services");
        return importedCount;
    }

    /// <summary>
    /// Import digital systems from analysis data
    /// </summary>
    private async Task<int> ImportDigitalSystemsInternal(List<string> systems)
    {
        if (systems == null || systems.Count == 0) return 0;

        int importedCount = 0;
        var existingSystems = await _context.SystemDefinitions.Select(s => s.NameAr).ToListAsync();

        foreach (var systemName in systems)
        {
            if (string.IsNullOrEmpty(systemName) || systemName == "لا يوجد") continue;

            if (existingSystems.Contains(systemName)) continue;

            var system = new SystemDefinition
            {
                Id = Guid.NewGuid().ToString(),
                NameAr = systemName,
                NameEn = systemName, // Use Arabic as English placeholder
                Code = GenerateUniqueCode("SYS", systemName),
                IsActive = true,
                DisplayOrder = importedCount + 1
            };

            _context.SystemDefinitions.Add(system);
            existingSystems.Add(systemName);
            importedCount++;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation($"Imported {importedCount} digital systems");
        return importedCount;
    }

    /// <summary>
    /// Import partners from analysis data (store as StrategicObjectives with Partner tag)
    /// </summary>
    private async Task<int> ImportPartnersInternal(List<string> partners)
    {
        if (partners == null || partners.Count == 0) return 0;

        int importedCount = 0;
        var existingPartners = await _context.StrategicObjectives
            .Where(s => s.Tags == "Partner")
            .Select(s => s.NameEn)
            .ToListAsync();

        foreach (var partnerName in partners)
        {
            if (string.IsNullOrEmpty(partnerName) || partnerName == "N/A" || partnerName == "--") continue;

            // Normalize partner name
            var normalizedName = partnerName.Trim();
            if (existingPartners.Any(p => p.Equals(normalizedName, StringComparison.OrdinalIgnoreCase))) continue;

            var partner = new StrategicObjective
            {
                Id = Guid.NewGuid().ToString(),
                NameEn = normalizedName,
                NameAr = normalizedName,
                Code = GenerateUniqueCode("PTR", normalizedName),
                Tags = "Partner",
                Level = 0, // External partner
                IsActive = true,
                DisplayOrder = importedCount + 1
            };

            _context.StrategicObjectives.Add(partner);
            existingPartners.Add(normalizedName);
            importedCount++;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation($"Imported {importedCount} partners");
        return importedCount;
    }

    /// <summary>
    /// Import projects from analysis data (store as StrategicObjectives with Project tag)
    /// </summary>
    private async Task<int> ImportProjectsInternal(List<string> projects)
    {
        if (projects == null || projects.Count == 0) return 0;

        int importedCount = 0;
        var existingProjects = await _context.StrategicObjectives
            .Where(s => s.Tags == "Project")
            .Select(s => s.NameEn)
            .ToListAsync();

        foreach (var projectName in projects)
        {
            if (string.IsNullOrEmpty(projectName)) continue;

            if (existingProjects.Any(p => p.Equals(projectName, StringComparison.OrdinalIgnoreCase))) continue;

            var project = new StrategicObjective
            {
                Id = Guid.NewGuid().ToString(),
                NameEn = projectName,
                NameAr = projectName,
                Code = GenerateUniqueCode("PRJ", projectName),
                Tags = "Project",
                Level = 1, // Strategic project
                IsActive = true,
                DisplayOrder = importedCount + 1
            };

            _context.StrategicObjectives.Add(project);
            existingProjects.Add(projectName);
            importedCount++;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation($"Imported {importedCount} projects");
        return importedCount;
    }

    /// <summary>
    /// Import Visio diagrams from extracted files and convert to BPMN
    /// </summary>
    [HttpPost("visio-diagrams")]
    public async Task<IActionResult> ImportVisioDiagrams()
    {
        try
        {
            // Path to extracted Visio files and mapping
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "ExtractedVisio");
            var mappingFile = Path.Combine(basePath, "sheet_visio_mapping.json");

            if (!System.IO.File.Exists(mappingFile))
            {
                return BadRequest(new { success = false, error = "Mapping file not found. Please extract Visio files first." });
            }

            // Read mapping
            var mappingJson = await System.IO.File.ReadAllTextAsync(mappingFile);
            var mappings = JsonSerializer.Deserialize<List<VisioMapping>>(mappingJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (mappings == null || mappings.Count == 0)
            {
                return BadRequest(new { success = false, error = "No mappings found in mapping file." });
            }

            // Get all ProcessTasks for matching
            var processTasks = await _context.ProcessTasks.ToListAsync();
            _logger.LogInformation($"Found {processTasks.Count} ProcessTasks and {mappings.Count} Visio mappings");

            var successCount = 0;
            var failCount = 0;
            var notMatchedCount = 0;
            var results = new List<object>();

            foreach (var mapping in mappings)
            {
                try
                {
                    var visioPath = Path.Combine(basePath, mapping.VisioFile);
                    if (!System.IO.File.Exists(visioPath))
                    {
                        _logger.LogWarning($"Visio file not found: {visioPath}");
                        failCount++;
                        continue;
                    }

                    // Find matching ProcessTask by Arabic name (exact or partial match)
                    var matchedTask = processTasks.FirstOrDefault(pt =>
                        pt.NameAr == mapping.SheetName ||
                        pt.NameAr?.StartsWith(mapping.SheetName) == true ||
                        mapping.SheetName.StartsWith(pt.NameAr ?? ""));

                    if (matchedTask == null)
                    {
                        _logger.LogWarning($"No matching ProcessTask for sheet: {mapping.SheetName}");
                        notMatchedCount++;
                        results.Add(new { sheet = mapping.SheetName, status = "no_match" });
                        continue;
                    }

                    // Extract XML content from VSDX (ZIP format)
                    string xmlContent;
                    using (var archive = ZipFile.OpenRead(visioPath))
                    {
                        // Try to find the main page XML
                        var pageEntry = archive.Entries.FirstOrDefault(e =>
                            e.FullName.Contains("pages/page", StringComparison.OrdinalIgnoreCase) &&
                            e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

                        if (pageEntry == null)
                            pageEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

                        if (pageEntry == null)
                        {
                            _logger.LogWarning($"No XML content found in Visio file: {mapping.VisioFile}");
                            failCount++;
                            continue;
                        }

                        using var entryStream = pageEntry.Open();
                        using var reader = new StreamReader(entryStream);
                        xmlContent = await reader.ReadToEndAsync();
                    }

                    // Convert to BPMN using AI service
                    var bpmnXml = await _aiService.ConvertVisioToBPMNAsync(xmlContent, $"Process: {mapping.SheetName}");
                    var cleanedBpmn = CleanBpmnXml(bpmnXml);

                    if (!string.IsNullOrWhiteSpace(cleanedBpmn) && LooksLikeBpmnXml(cleanedBpmn))
                    {
                        matchedTask.BpmnDiagram = cleanedBpmn;
                        successCount++;
                        results.Add(new { sheet = mapping.SheetName, task = matchedTask.NameAr, status = "success" });
                        _logger.LogInformation($"Converted: {mapping.SheetName} -> {matchedTask.NameAr}");
                    }
                    else
                    {
                        failCount++;
                        results.Add(new { sheet = mapping.SheetName, status = "conversion_failed" });
                        _logger.LogWarning($"BPMN conversion failed for: {mapping.SheetName}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing {mapping.SheetName}");
                    failCount++;
                    results.Add(new { sheet = mapping.SheetName, status = "error", error = ex.Message });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Import completed. Success: {successCount}, Failed: {failCount}, Not matched: {notMatchedCount}",
                successCount,
                failCount,
                notMatchedCount,
                totalMappings = mappings.Count,
                details = results.Take(50) // Limit details to first 50 for response size
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Visio diagrams");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Import a single Visio diagram for a specific ProcessTask
    /// </summary>
    [HttpPost("visio-single/{processTaskId}")]
    public async Task<IActionResult> ImportSingleVisioDiagram(string processTaskId, IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, error = "No file uploaded." });

            var processTask = await _context.ProcessTasks.FindAsync(processTaskId);
            if (processTask == null)
                return NotFound(new { success = false, error = "ProcessTask not found." });

            // Extract XML from VSDX
            string xmlContent;
            using var stream = file.OpenReadStream();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var pageEntry = archive.Entries.FirstOrDefault(e =>
                e.FullName.Contains("pages/page", StringComparison.OrdinalIgnoreCase) &&
                e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

            if (pageEntry == null)
                pageEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

            if (pageEntry == null)
                return BadRequest(new { success = false, error = "Could not find page content in Visio file." });

            using var entryStream = pageEntry.Open();
            using var reader = new StreamReader(entryStream);
            xmlContent = await reader.ReadToEndAsync();

            // Convert to BPMN
            var bpmnXml = await _aiService.ConvertVisioToBPMNAsync(xmlContent, $"Process: {processTask.NameAr ?? processTask.NameEn}");
            var cleanedBpmn = CleanBpmnXml(bpmnXml);

            if (!string.IsNullOrWhiteSpace(cleanedBpmn) && LooksLikeBpmnXml(cleanedBpmn))
            {
                processTask.BpmnDiagram = cleanedBpmn;
                await _context.SaveChangesAsync();
                return Ok(new { success = true, bpmnXml = cleanedBpmn });
            }
            else
            {
                return BadRequest(new { success = false, error = "BPMN conversion failed.", raw = bpmnXml });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing single Visio diagram");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    private static bool ContainsArabicScript(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (var c in text)
        {
            if (c >= '\u0600' && c <= '\u06FF') return true;
            if (c >= '\u0750' && c <= '\u077F') return true;
            if (c >= '\u08A0' && c <= '\u08FF') return true;
        }
        return false;
    }

    private static bool LooksLikeBpmnXml(string text)
    {
        var t = (text ?? string.Empty).TrimStart();
        return t.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("<bpmn:definitions", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("<definitions", StringComparison.OrdinalIgnoreCase)
               || t.Contains("<bpmn:definitions", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanBpmnXml(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var text = raw.Trim();
        text = text.Replace("\r\n", "\n");

        // Strip ``` fences
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var endFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (endFence > 3)
            {
                var firstNewline = text.IndexOf('\n');
                if (firstNewline > 0 && firstNewline < endFence)
                {
                    text = text.Substring(firstNewline + 1, endFence - firstNewline - 1).Trim();
                }
            }
        }

        // Find XML start
        var xmlStart = text.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase);
        if (xmlStart < 0)
            xmlStart = text.IndexOf("<bpmn:definitions", StringComparison.OrdinalIgnoreCase);
        if (xmlStart < 0)
            xmlStart = text.IndexOf("<definitions", StringComparison.OrdinalIgnoreCase);

        if (xmlStart > 0)
            text = text.Substring(xmlStart);

        return text.Trim();
    }

    // Normalizes Arabic/Latin strings for fuzzy matching: collapses whitespace (incl. NBSP/tatweel),
    // unifies common Arabic variants (alef forms, yaa, taa marbuta), lowercases Latin.
    private static string NormalizeForMatch(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var sb = new System.Text.StringBuilder(s.Length);
        bool lastWasSpace = false;
        foreach (var ch in s.Trim())
        {
            char c = ch;
            if (c == '\u0640') continue; // tatweel
            if (c == '\u00A0' || char.IsWhiteSpace(c)) { if (!lastWasSpace) { sb.Append(' '); lastWasSpace = true; } continue; }
            lastWasSpace = false;
            if (c == '\u0622' || c == '\u0623' || c == '\u0625') c = '\u0627'; // Alef variants -> Alef
            else if (c == '\u0649') c = '\u064A'; // Alef Maksura -> Yaa
            else if (c == '\u0629') c = '\u0647'; // Taa Marbuta -> Haa
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    // Similarity ratio in [0,1] based on Levenshtein distance.
    private static double LevenshteinRatio(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;
        int n = a.Length, m = b.Length;
        var d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;
        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        int max = Math.Max(n, m);
        return max == 0 ? 1.0 : 1.0 - ((double)d[n, m] / max);
    }

    // Ladder matcher used by the BPMN importer. Returns the first candidate that hits
    // via (1) Code exact, (2) normalized NameAr/NameEn equality or either-way substring,
    // or (3) Levenshtein similarity >= fuzzyThreshold on NameAr or NameEn. Returns
    // (null, "") if nothing matches. matchHow reports which tier produced the hit.
    private static (T? match, string matchHow) FindBestMatch<T>(
        IEnumerable<T> candidates,
        string rawKey,
        string normKey,
        double fuzzyThreshold,
        Func<T, string?> codeSelector,
        Func<T, string?> nameArSelector,
        Func<T, string?> nameEnSelector) where T : class
    {
        var rawTrimmed = rawKey.Trim();

        var codeHit = candidates.FirstOrDefault(c =>
        {
            var code = codeSelector(c);
            return !string.IsNullOrWhiteSpace(code) && code.Trim().Equals(rawTrimmed, StringComparison.OrdinalIgnoreCase);
        });
        if (codeHit != null) return (codeHit, "code_exact");

        var substrHit = candidates.FirstOrDefault(c =>
        {
            var arNorm = NormalizeForMatch(nameArSelector(c));
            var enNorm = NormalizeForMatch(nameEnSelector(c));
            return IsNormalizedSubstringMatch(arNorm, normKey) || IsNormalizedSubstringMatch(enNorm, normKey);
        });
        if (substrHit != null) return (substrHit, "name_substring");

        T? bestFuzzy = null;
        double bestScore = 0;
        foreach (var c in candidates)
        {
            var arScore = LevenshteinRatio(NormalizeForMatch(nameArSelector(c)), normKey);
            var enScore = LevenshteinRatio(NormalizeForMatch(nameEnSelector(c)), normKey);
            var score = Math.Max(arScore, enScore);
            if (score > bestScore) { bestScore = score; bestFuzzy = c; }
        }
        if (bestFuzzy != null && bestScore >= fuzzyThreshold)
            return (bestFuzzy, $"fuzzy_{bestScore:F2}");

        return (null, "");
    }

    private static bool IsNormalizedSubstringMatch(string candidateNorm, string keyNorm) =>
        !string.IsNullOrEmpty(candidateNorm) && !string.IsNullOrEmpty(keyNorm)
        && (candidateNorm == keyNorm || candidateNorm.Contains(keyNorm) || keyNorm.Contains(candidateNorm));

    // Opt-in flow for the batch importer: when no existing Process/ProcessTask
    // matches a sheet, we can CREATE one on the fly instead of dropping the
    // BPMN on disk with no DB linkage. Used when the xlsx is canonical and
    // the catalog hasn't been pre-seeded. Idempotent: same sheet name returns
    // the same Process (so re-running the batch doesn't pile up duplicates).
    //
    // Quality gate (FIX-2): returns null when the sheet name fails the same
    // ≥3-char rule the manual Create form enforces (FIX-1). The caller
    // records the skip in the per-sheet result so users see why the BPMN
    // was dropped instead of catalogued.
    private async Task<ESEMS.Web.Models.APQC.Process?> GetOrCreateAutoImportedProcessAsync(string sheetName)
    {
        const string AutoCode = "AUTO-IMPORT";

        // FIX-2: reject names that the manual form would reject.
        if (string.IsNullOrWhiteSpace(sheetName) || sheetName.Trim().Length < 3)
            return null;

        // Idempotency: already got a Process with this sheet name?
        var existing = await _context.Processes.FirstOrDefaultAsync(p => p.NameAr == sheetName)
            ?? _context.Processes.Local.FirstOrDefault(p => p.NameAr == sheetName);
        if (existing != null) return existing;

        // Ensure the Auto-Import parent chain (Category → ProcessGroup) exists.
        var autoCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Code == AutoCode)
            ?? _context.Categories.Local.FirstOrDefault(c => c.Code == AutoCode);
        if (autoCategory == null)
        {
            autoCategory = new ESEMS.Web.Models.APQC.Category
            {
                Id = Guid.NewGuid().ToString(),
                Code = AutoCode,
                NameAr = "مستورد تلقائياً",
                NameEn = "Auto-Imported from Visio",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Categories.Add(autoCategory);
        }

        var autoGroup = await _context.ProcessGroups.FirstOrDefaultAsync(g => g.Code == AutoCode)
            ?? _context.ProcessGroups.Local.FirstOrDefault(g => g.Code == AutoCode);
        if (autoGroup == null)
        {
            autoGroup = new ESEMS.Web.Models.APQC.ProcessGroup
            {
                Id = Guid.NewGuid().ToString(),
                Code = AutoCode,
                CategoryId = autoCategory.Id,
                NameAr = "مستورد تلقائياً",
                NameEn = "Auto-Imported from Visio",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ProcessGroups.Add(autoGroup);
        }

        // FIX-2: every auto-import row gets the same OwningUnit — the catalog
        // is no longer salted with units of "—". The unit lives behind the
        // AutoCode marker so a single follow-up edit can re-home all
        // auto-imported processes once a real owner is identified.
        var autoUnit = await _context.OrganizationUnits.FirstOrDefaultAsync(u => u.Code == AutoCode)
            ?? _context.OrganizationUnits.Local.FirstOrDefault(u => u.Code == AutoCode);
        if (autoUnit == null)
        {
            autoUnit = new ESEMS.Web.Models.APQC.OrganizationUnit
            {
                // Id is a DB identity now — never assign client-side.
                Code = AutoCode,
                NameAr = "وحدة الاستيراد التلقائي",
                NameEn = "Auto-Import Holding Unit",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.OrganizationUnits.Add(autoUnit);
        }

        // Deterministic per-sheet code so re-imports keep the same Code.
        var codeHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(sheetName))).Substring(0, 8);

        var process = new ESEMS.Web.Models.APQC.Process
        {
            Id = Guid.NewGuid().ToString(),
            Code = $"AUTO-{codeHash}",
            ProcessGroupId = autoGroup.Id,
            // OwningUnit.Id is a DB identity that may be unassigned until the
            // batch SaveChanges; wire the navigation so EF resolves the int FK.
            OwningUnit = autoUnit,
            NameAr = sheetName,
            NameEn = sheetName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Processes.Add(process);
        return process;
    }

    // Creates a ProcessBpmnVersion row for an import match. Unlike
    // ProcessesController.CreateBpmnVersionRecord (which runs in a logged-in
    // edit context), this runs from an [AllowAnonymous] batch endpoint, so
    // the created-by fields fall back to "System/Import". Returns the new
    // version number so the caller can surface it in per-sheet results.
    //
    // IMPORTANT: the DB SaveChangesAsync runs once at the end of the batch.
    // If we only query _context.ProcessBpmnVersions.Where(...) we'd miss
    // rows added earlier in this same batch that haven't been committed yet,
    // so two sheets matching the same Process would both get VersionNumber 1
    // and the final SaveChanges would explode with a unique-key violation.
    // Merging the tracked Local set fixes that.
    private async Task<int> CreateBpmnVersionFromImportAsync(string processId, string bpmnXml, string sheetName, string matchHow)
    {
        var dbVersions = await _context.ProcessBpmnVersions
            .Where(v => v.ProcessId == processId)
            .ToListAsync();

        var pendingVersions = _context.ProcessBpmnVersions.Local
            .Where(v => v.ProcessId == processId)
            .ToList();

        // Local may include the rows we just loaded from DB (since the query
        // tracks them). Dedup by Id so we don't double-count.
        var allVersions = dbVersions
            .Concat(pendingVersions.Where(p => dbVersions.All(d => d.Id != p.Id)))
            .ToList();

        foreach (var v in allVersions) v.IsCurrent = false;

        var nextVersionNumber = allVersions.Count > 0
            ? allVersions.Max(v => v.VersionNumber) + 1
            : 1;

        var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userName = User?.Identity?.IsAuthenticated == true
            ? (User.Identity.Name ?? "System")
            : "System/Import";

        _context.ProcessBpmnVersions.Add(new Models.APQC.ProcessBpmnVersion
        {
            Id = Guid.NewGuid().ToString(),
            ProcessId = processId,
            VersionNumber = nextVersionNumber,
            BpmnXml = bpmnXml,
            ChangeDescription = $"Imported from Visio sheet '{sheetName}' (match: {matchHow})",
            CreatedById = userId,
            CreatedByName = userName,
            CreatedAt = DateTime.UtcNow,
            IsCurrent = true,
            XmlSizeBytes = System.Text.Encoding.UTF8.GetByteCount(bpmnXml)
        });

        return nextVersionNumber;
    }

    /// <summary>
    /// Import BPMN files from Python-converted folder (ConvertedBPMN)
    /// This imports the properly converted BPMN files from Visio
    /// </summary>
    [HttpPost("converted-bpmn")]
    public async Task<IActionResult> ImportConvertedBpmn()
    {
        try
        {
            // Paths to converted BPMN files and mapping
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..");
            var bpmnFolder = Path.Combine(basePath, "ConvertedBPMN");
            var mappingFile = Path.Combine(basePath, "ExtractedVisio", "sheet_visio_mapping.json");

            if (!Directory.Exists(bpmnFolder))
            {
                return BadRequest(new { success = false, error = "ConvertedBPMN folder not found. Run the Python conversion script first." });
            }

            if (!System.IO.File.Exists(mappingFile))
            {
                return BadRequest(new { success = false, error = "Mapping file not found." });
            }

            // Read mapping
            var mappingJson = await System.IO.File.ReadAllTextAsync(mappingFile);
            var mappings = JsonSerializer.Deserialize<List<VisioMapping>>(mappingJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (mappings == null || mappings.Count == 0)
            {
                return BadRequest(new { success = false, error = "No mappings found." });
            }

            // Get all ProcessTasks
            var processTasks = await _context.ProcessTasks.ToListAsync();
            _logger.LogInformation($"Found {processTasks.Count} ProcessTasks and {mappings.Count} mappings");

            var successCount = 0;
            var failCount = 0;
            var results = new List<object>();

            foreach (var mapping in mappings)
            {
                // Find matching ProcessTask by sheet name
                var matchingTask = processTasks.FirstOrDefault(pt =>
                    pt.NameAr?.Contains(mapping.SheetName, StringComparison.OrdinalIgnoreCase) == true ||
                    mapping.SheetName.Contains(pt.NameAr ?? "", StringComparison.OrdinalIgnoreCase) ||
                    pt.NameEn?.Contains(mapping.SheetName, StringComparison.OrdinalIgnoreCase) == true
                );

                if (matchingTask == null)
                {
                    // Try partial match
                    var sheetWords = mapping.SheetName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (sheetWords.Length >= 2)
                    {
                        matchingTask = processTasks.FirstOrDefault(pt =>
                            sheetWords.Any(w => pt.NameAr?.Contains(w, StringComparison.OrdinalIgnoreCase) == true));
                    }
                }

                if (matchingTask != null)
                {
                    // Get the BPMN file name (replace .vsdx with .bpmn)
                    var bpmnFileName = Path.ChangeExtension(mapping.VisioFile, ".bpmn");
                    var bpmnFilePath = Path.Combine(bpmnFolder, bpmnFileName);

                    if (System.IO.File.Exists(bpmnFilePath))
                    {
                        var bpmnXml = await System.IO.File.ReadAllTextAsync(bpmnFilePath);

                        if (!string.IsNullOrWhiteSpace(bpmnXml) && LooksLikeBpmnXml(bpmnXml))
                        {
                            matchingTask.BpmnDiagram = bpmnXml;
                            successCount++;
                            results.Add(new { task = matchingTask.NameAr, bpmnFile = bpmnFileName, status = "success" });
                        }
                        else
                        {
                            failCount++;
                            results.Add(new { task = matchingTask.NameAr, bpmnFile = bpmnFileName, status = "invalid_bpmn" });
                        }
                    }
                    else
                    {
                        failCount++;
                        results.Add(new { sheetName = mapping.SheetName, bpmnFile = bpmnFileName, status = "file_not_found" });
                    }
                }
                else
                {
                    failCount++;
                    results.Add(new { sheetName = mapping.SheetName, status = "no_matching_task" });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                imported = successCount,
                failed = failCount,
                total = mappings.Count,
                results = results.Take(20) // Limit output
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing converted BPMN files");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Extract embedded Visio drawings from full.xlsx, convert each to BPMN 2.0 XML
    /// using AWS Bedrock, and store in matching ProcessTask records.
    /// </summary>
    /// <summary>
    /// Lightweight progress snapshot of the BPMN batch import output folder.
    /// The admin UI polls this while the main endpoint is blocked on Bedrock/
    /// OpenAI calls. Returns file count + timestamps without touching the DB.
    /// </summary>
    [HttpGet("excel-visio-to-bpmn/progress")]
    public IActionResult BpmnImportProgress()
    {
        var outputFolder = Path.Combine(Directory.GetCurrentDirectory(), "..", "ConvertedBPMN_Bedrock");
        if (!Directory.Exists(outputFolder))
            return Ok(new { exists = false, fileCount = 0, firstAt = (DateTime?)null, lastAt = (DateTime?)null, lastFileName = (string?)null, ageSeconds = 0.0 });

        var files = new DirectoryInfo(outputFolder).GetFiles("*.bpmn");
        if (files.Length == 0)
            return Ok(new { exists = true, fileCount = 0, firstAt = (DateTime?)null, lastAt = (DateTime?)null, lastFileName = (string?)null, ageSeconds = 0.0 });

        var ordered = files.OrderBy(f => f.LastWriteTimeUtc).ToArray();
        var last = ordered[^1];
        var ageSeconds = (DateTime.UtcNow - last.LastWriteTimeUtc).TotalSeconds;
        return Ok(new
        {
            exists = true,
            fileCount = files.Length,
            firstAt = ordered[0].LastWriteTimeUtc,
            lastAt = last.LastWriteTimeUtc,
            lastFileName = last.Name,
            ageSeconds
        });
    }

    [HttpPost("excel-visio-to-bpmn")]
    public async Task<IActionResult> ImportExcelVisioBpmn([FromQuery] int? startIndex = null, [FromQuery] int? count = null, [FromQuery] bool autoCreate = false)
    {
        var tempPath = string.Empty;
        try
        {
            var excelPath = Path.Combine(Directory.GetCurrentDirectory(), "full.xlsx");
            if (!System.IO.File.Exists(excelPath))
                return BadRequest(new { success = false, error = "full.xlsx not found in project root." });

            // Copy to temp to avoid file-lock issues (file may be open in Excel)
            tempPath = Path.Combine(Path.GetTempPath(), $"esems_import_{Guid.NewGuid():N}.xlsx");
            System.IO.File.Copy(excelPath, tempPath, overwrite: true);

            // Get all ProcessTasks for matching
            var processTasks = await _context.ProcessTasks.ToListAsync();
            _logger.LogInformation("Found {Count} ProcessTasks for matching. Sample NameAr/Code: {Samples}",
                processTasks.Count,
                string.Join(" | ", processTasks.Take(5).Select(pt => $"[{pt.Code}] {pt.NameAr}")));

            // Step 1: Open xlsx as ZIP and build sheet-name-to-Visio mapping
            var sheetVisioMap = new Dictionary<string, byte[]>(); // sheetName -> vsdx bytes

            using (var xlsxArchive = ZipFile.OpenRead(tempPath))
            {
                // Parse workbook.xml to get sheet names by rId
                var workbookEntry = xlsxArchive.GetEntry("xl/workbook.xml");
                if (workbookEntry == null)
                    return BadRequest(new { success = false, error = "Invalid xlsx: no workbook.xml" });

                var sheetNames = new List<string>(); // ordered by sheet index
                var sheetRIds = new List<string>();
                using (var wbStream = workbookEntry.Open())
                {
                    var wbDoc = XDocument.Load(wbStream);
                    var ns = wbDoc.Root!.GetDefaultNamespace();
                    var rNs = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                    var sheets = wbDoc.Descendants(ns + "sheet").ToList();
                    foreach (var sheet in sheets)
                    {
                        sheetNames.Add(sheet.Attribute("name")?.Value ?? "");
                        sheetRIds.Add(sheet.Attribute(rNs + "id")?.Value ?? "");
                    }
                }

                // Parse workbook.xml.rels to map rId -> sheetN.xml
                var wbRelsEntry = xlsxArchive.GetEntry("xl/_rels/workbook.xml.rels");
                var rIdToSheetFile = new Dictionary<string, string>(); // rId -> "worksheets/sheet1.xml"
                if (wbRelsEntry != null)
                {
                    using var relsStream = wbRelsEntry.Open();
                    var relsDoc = XDocument.Load(relsStream);
                    var relsNs = relsDoc.Root!.GetDefaultNamespace();
                    foreach (var rel in relsDoc.Descendants(relsNs + "Relationship"))
                    {
                        var id = rel.Attribute("Id")?.Value ?? "";
                        var target = rel.Attribute("Target")?.Value ?? "";
                        if (target.Contains("worksheets/"))
                            rIdToSheetFile[id] = target;
                    }
                }

                // Build sheetFile -> sheetName lookup
                var sheetFileToName = new Dictionary<string, string>();
                for (int i = 0; i < sheetNames.Count && i < sheetRIds.Count; i++)
                {
                    if (rIdToSheetFile.TryGetValue(sheetRIds[i], out var file))
                        sheetFileToName[file] = sheetNames[i];
                }

                // For each sheet, check its _rels for an embedded Visio
                foreach (var kvp in sheetFileToName)
                {
                    var sheetFile = kvp.Key; // e.g. "worksheets/sheet5.xml"
                    var sheetName = kvp.Value;

                    // Sheet rels path: xl/worksheets/_rels/sheet5.xml.rels
                    var sheetFileName = Path.GetFileName(sheetFile);
                    var sheetRelsPath = $"xl/worksheets/_rels/{sheetFileName}.rels";
                    var sheetRelsEntry = xlsxArchive.GetEntry(sheetRelsPath);
                    if (sheetRelsEntry == null) continue;

                    string? visioEmbeddingPath = null;
                    using (var sRelsStream = sheetRelsEntry.Open())
                    {
                        var sRelsDoc = XDocument.Load(sRelsStream);
                        var sRelsNs = sRelsDoc.Root!.GetDefaultNamespace();
                        // Look for oleObject or package relationship pointing to a .vsdx
                        foreach (var rel in sRelsDoc.Descendants(sRelsNs + "Relationship"))
                        {
                            var target = rel.Attribute("Target")?.Value ?? "";
                            if (target.EndsWith(".vsdx", StringComparison.OrdinalIgnoreCase))
                            {
                                // Target is relative to xl/worksheets/, resolve to full path
                                visioEmbeddingPath = "xl/" + target.TrimStart('.', '/').Replace("../", "");
                                // Normalize: ../embeddings/foo.vsdx -> xl/embeddings/foo.vsdx
                                if (target.StartsWith("../"))
                                    visioEmbeddingPath = "xl/" + target.Substring(3);
                                break;
                            }
                        }
                    }

                    if (visioEmbeddingPath == null) continue;

                    // Read the embedded .vsdx bytes
                    var visioEntry = xlsxArchive.GetEntry(visioEmbeddingPath);
                    if (visioEntry == null) continue;

                    using var vStream = visioEntry.Open();
                    using var ms = new MemoryStream();
                    await vStream.CopyToAsync(ms);
                    sheetVisioMap[sheetName] = ms.ToArray();
                }
            }

            _logger.LogInformation("Extracted {Count} sheet-to-Visio mappings from xlsx. Sample sheet names: {Samples}",
                sheetVisioMap.Count,
                string.Join(" | ", sheetVisioMap.Keys.Take(5)));

            // Apply pagination
            var orderedEntries = sheetVisioMap.OrderBy(kv => kv.Key).ToList();
            var start = startIndex ?? 0;
            var take = count ?? orderedEntries.Count;
            var batch = orderedEntries.Skip(start).Take(take).ToList();

            // Also load Processes for matching
            var processes = await _context.Processes.ToListAsync();
            _logger.LogInformation("Found {Count} Processes for matching. Sample NameAr/Code: {Samples}",
                processes.Count,
                string.Join(" | ", processes.Take(5).Select(p => $"[{p.Code}] {p.NameAr}")));

            // Output folder for BPMN files
            var outputFolder = Path.Combine(Directory.GetCurrentDirectory(), "..", "ConvertedBPMN_Bedrock");
            Directory.CreateDirectory(outputFolder);

            // Step 2: Convert each Visio to BPMN via Bedrock
            var successCount = 0;
            var failCount = 0;
            var dbMatchCount = 0;
            var retrySucceededCount = 0;
            var taskPromotionCount = 0;
            var versionedCount = 0;
            var diRefRewriteCount = 0;
            var autoCreatedCount = 0;
            var results = new List<object>();

            // Pairs of (Process.Id, bpmnXml) queued for the post-loop lane
            // reconciliation pass. We defer the reconcile until AFTER the
            // outer SaveChanges so every Process has a stable Id and its
            // final BpmnDiagram persisted before BpmnLane FKs are written.
            var laneReconcileQueue = new List<(string processId, string bpmnXml)>();

            foreach (var (sheetName, vsdxBytes) in batch)
            {
                try
                {
                    // Extract page1.xml from the .vsdx (which is also a ZIP)
                    string? visioXml = null;
                    using (var vsdxStream = new MemoryStream(vsdxBytes))
                    using (var vsdxArchive = new ZipArchive(vsdxStream, ZipArchiveMode.Read))
                    {
                        var pageEntry = vsdxArchive.Entries.FirstOrDefault(e =>
                            e.FullName.Contains("pages/page", StringComparison.OrdinalIgnoreCase) &&
                            e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

                        if (pageEntry != null)
                        {
                            using var pageStream = pageEntry.Open();
                            using var reader = new StreamReader(pageStream);
                            visioXml = await reader.ReadToEndAsync();
                        }
                    }

                    if (string.IsNullOrWhiteSpace(visioXml))
                    {
                        failCount++;
                        results.Add(new { sheet = sheetName, status = "no_visio_xml" });
                        _logger.LogWarning("No page XML found in Visio for sheet: {Sheet}", sheetName);
                        continue;
                    }

                    // Mirror the /AI/Diagrams pipeline: analyse Visio → improve
                    // prompt → generate diagram. Produces the same "two pools
                    // (Customer + MBRHE-with-lanes) + messageFlow" shape as
                    // the manual AI diagrams flow, instead of the old direct
                    // Visio→BPMN call.
                    var extracted = _visioExtractor.Extract(visioXml, sheetName);
                    var sheetIsArabic = ContainsArabicScript(sheetName) || ContainsArabicScript(extracted.Description);

                    var improveSystem = _analysisService.BuildOptimizedBpmnPrompt(sheetIsArabic);
                    var improveUser = sheetIsArabic
                        ? $"الوصف المستخرج من مخطط Visio:\n\"{extracted.Description}\"\n\nقم بتحسين هذا الوصف لتوليد مخطط BPMN أفضل وأكثر تفصيلاً. يجب أن يكون الرد باللغة العربية فقط."
                        : $"Process description extracted from the source Visio diagram:\n\"{extracted.Description}\"\n\nOptimize this description for BPMN generation. Be specific about roles, swimlanes, decisions, and parallel branches. Keep the core intent.";
                    string improvedDescription;
                    try
                    {
                        var improveRaw = await _aiService.ChatAsync(improveUser, new List<(string role, string content)> { ("system", improveSystem) });
                        improvedDescription = _analysisService.CleanOptimizedPromptResponse(improveRaw);
                        if (string.IsNullOrWhiteSpace(improvedDescription))
                            improvedDescription = extracted.Description;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Prompt-improvement step failed for '{Sheet}', falling back to raw extracted description.", sheetName);
                        improvedDescription = extracted.Description;
                    }

                    var bpmnXml = await _aiService.GenerateBPMNDiagramAsync(
                        sheetName,
                        improvedDescription,
                        extracted.Steps?.ToList(),
                        hintArabic: sheetIsArabic);
                    var cleanedBpmn = CleanBpmnXml(bpmnXml);

                    if (string.IsNullOrWhiteSpace(cleanedBpmn) || !LooksLikeBpmnXml(cleanedBpmn))
                    {
                        failCount++;
                        results.Add(new { sheet = sheetName, status = "conversion_failed", preview = bpmnXml?.Substring(0, Math.Min(200, bpmnXml?.Length ?? 0)) });
                        _logger.LogWarning("BPMN conversion failed for: {Sheet}", sheetName);
                        continue;
                    }

                    // Run the DI-ref suffix heal FIRST, on the original
                    // (possibly Arabic) ids — it has a better shot at
                    // matching a fabricated <BPMNEdge bpmnElement='Flow_X'/>
                    // to an existing <sequenceFlow id='Flow_Y'/> while the
                    // ids still carry meaningful Arabic suffixes. After
                    // SanitizeIds renames everything to generic
                    // `sequenceFlow_1` etc., suffix matching becomes
                    // useless.
                    var diFix = _bpmnPostProcessor.FixDiRefs(cleanedBpmn);
                    if (diFix.Rewrites > 0)
                    {
                        cleanedBpmn = diFix.BpmnXml;
                        diRefRewriteCount += diFix.Rewrites;
                        _logger.LogInformation("Auto-healed {N} DI ref(s) for '{Sheet}' via suffix match.", diFix.Rewrites, sheetName);
                    }

                    // Sanitize IDs. LLMs frequently embed Arabic/CJK text in
                    // element IDs (e.g. "Process_إدارة_التغيير"), which
                    // parses at the XML layer but makes bpmn-js refuse to
                    // render ("illegal ID").
                    var idSanitize = _bpmnPostProcessor.SanitizeIds(cleanedBpmn);
                    if (idSanitize.IdsRewritten > 0)
                    {
                        cleanedBpmn = idSanitize.BpmnXml;
                        _logger.LogInformation("Sanitized {N} non-ASCII id(s) for '{Sheet}'.", idSanitize.IdsRewritten, sheetName);
                    }

                    var validation = _bpmnValidator.Validate(cleanedBpmn);
                    var retried = false;
                    if (!validation.IsValid && validation.Errors.Count > 0)
                    {
                        _logger.LogInformation("BPMN validation failed for '{Sheet}' ({Reason}). Retrying once with error feedback.",
                            sheetName, validation.Reason);
                        // Retry via the same AI Diagrams pipeline. Append the
                        // validator errors as a note so the generator steers
                        // around the same issues on the second attempt.
                        var retryDesc = improvedDescription
                            + "\n\nThe previous generation had these structural errors — avoid them:\n  - "
                            + string.Join("\n  - ", validation.Errors.Take(6));
                        var retryXml = await _aiService.GenerateBPMNDiagramAsync(
                            sheetName,
                            retryDesc,
                            extracted.Steps?.ToList(),
                            hintArabic: sheetIsArabic);
                        var retryCleaned = CleanBpmnXml(retryXml);
                        if (!string.IsNullOrWhiteSpace(retryCleaned) && LooksLikeBpmnXml(retryCleaned))
                        {
                            var retrySanitize = _bpmnPostProcessor.SanitizeIds(retryCleaned);
                            if (retrySanitize.IdsRewritten > 0) retryCleaned = retrySanitize.BpmnXml;

                            // Apply the same auto-heal pass to the retry output.
                            var retryDiFix = _bpmnPostProcessor.FixDiRefs(retryCleaned);
                            if (retryDiFix.Rewrites > 0)
                            {
                                retryCleaned = retryDiFix.BpmnXml;
                                diRefRewriteCount += retryDiFix.Rewrites;
                            }
                            var retryValidation = _bpmnValidator.Validate(retryCleaned);
                            if (retryValidation.IsValid)
                            {
                                cleanedBpmn = retryCleaned;
                                validation = retryValidation;
                                retried = true;
                            }
                            else
                            {
                                validation = retryValidation; // use the newer errors for reporting
                            }
                        }
                    }

                    if (!validation.IsValid)
                    {
                        failCount++;
                        results.Add(new
                        {
                            sheet = sheetName,
                            status = "validation_failed",
                            reason = validation.Reason,
                            errors = validation.Errors.Take(5).ToList(),
                            flowNodes = validation.FlowNodeCount,
                            diOrphans = validation.DiOrphanCount,
                            retryAttempted = true
                        });
                        _logger.LogWarning("BPMN validation failed for '{Sheet}' after retry ({Reason}): {Errors}",
                            sheetName, validation.Reason, string.Join("; ", validation.Errors.Take(3)));
                        continue;
                    }
                    if (retried)
                    {
                        retrySucceededCount++;
                        _logger.LogInformation("BPMN retry succeeded for '{Sheet}' ({Flows} flows, {Shapes} shapes).",
                            sheetName, validation.FlowNodeCount, validation.DiShapeCount);
                    }
                    if (validation.Warnings.Count > 0)
                    {
                        _logger.LogInformation("BPMN warnings for '{Sheet}': {Warnings}",
                            sheetName, string.Join("; ", validation.Warnings));
                    }

                    // Post-process: promote generic <task> to <userTask>/<serviceTask>
                    // based on high-confidence name heuristics. Purely visual — renders
                    // a person/gear icon instead of a plain box.
                    var postProcessed = _bpmnPostProcessor.UpgradeTaskTypes(cleanedBpmn);
                    if (postProcessed.TotalPromotions > 0)
                    {
                        cleanedBpmn = postProcessed.BpmnXml;
                        taskPromotionCount += postProcessed.TotalPromotions;
                        _logger.LogInformation("Post-process for '{Sheet}': {UserTasks} userTask, {ServiceTasks} serviceTask promotions.",
                            sheetName, postProcessed.UserTaskPromotions, postProcessed.ServiceTaskPromotions);
                    }

                    // Save BPMN file to disk (always, regardless of DB match)
                    var safeFileName = string.Join("_", sheetName.Split(Path.GetInvalidFileNameChars()));
                    var bpmnFilePath = Path.Combine(outputFolder, $"{safeFileName}.bpmn");
                    await System.IO.File.WriteAllTextAsync(bpmnFilePath, cleanedBpmn);
                    successCount++;

                    // Try to match to DB. Ladder (first hit wins):
                    //   1. Code exact (trimmed, case-insensitive)
                    //   2. Normalized NameAr / NameEn equality or substring either way
                    //   3. Fuzzy auto-accept: best Levenshtein ratio >= 0.85 on NameAr or NameEn
                    // ProcessTask tried before Process (more specific).
                    var sheetNorm = NormalizeForMatch(sheetName);
                    const double FuzzyThreshold = 0.85;

                    var (matchedTask, taskMatchHow) = FindBestMatch(
                        processTasks, sheetName, sheetNorm, FuzzyThreshold,
                        pt => pt.Code, pt => pt.NameAr, pt => pt.NameEn);

                    if (matchedTask != null)
                    {
                        matchedTask.BpmnDiagram = cleanedBpmn;
                        dbMatchCount++;
                        // ProcessTask-level BPMN has no version history table — we overwrite
                        // and rely on the written .bpmn file in outputFolder as the audit trail.
                        results.Add(new { sheet = sheetName, file = bpmnFilePath, matchType = "ProcessTask", matchHow = taskMatchHow, matchCode = matchedTask.Code, matchName = matchedTask.NameAr, versioned = false, status = "success" });
                    }
                    else
                    {
                        var (matchedProcess, procMatchHow) = FindBestMatch(
                            processes, sheetName, sheetNorm, FuzzyThreshold,
                            p => p.Code, p => p.NameAr, p => p.NameEn);

                        if (matchedProcess != null)
                        {
                            matchedProcess.BpmnDiagram = cleanedBpmn;
                            dbMatchCount++;
                            var versionNumber = await CreateBpmnVersionFromImportAsync(matchedProcess.Id, cleanedBpmn, sheetName, procMatchHow);
                            versionedCount++;
                            laneReconcileQueue.Add((matchedProcess.Id, cleanedBpmn));
                            results.Add(new { sheet = sheetName, file = bpmnFilePath, matchType = "Process", matchHow = procMatchHow, matchCode = matchedProcess.Code, matchName = matchedProcess.NameAr, versioned = true, versionNumber, status = "success" });
                        }
                        else if (autoCreate)
                        {
                            // autoCreate mode: make a fresh Process row from the sheet
                            // so every import populates the catalog instead of
                            // dropping unmatched sheets on the floor.
                            var created = await GetOrCreateAutoImportedProcessAsync(sheetName);
                            if (created == null)
                            {
                                // FIX-2: quality gate rejected this sheet name. Record
                                // the skip so the per-sheet result explains why no
                                // Process was created instead of silently dropping it.
                                failCount++;
                                results.Add(new { sheet = sheetName, file = bpmnFilePath, matchType = "Process", matchHow = "auto_create_rejected", reason = "sheet name fails ≥3-char rule", status = "failed" });
                            }
                            else
                            {
                                created.BpmnDiagram = cleanedBpmn;
                                // Add it to the processes list so later sheets with the same
                                // name don't create duplicates AND can fuzzy-match to it.
                                if (!processes.Any(p => p.Id == created.Id)) processes.Add(created);
                                dbMatchCount++;
                                autoCreatedCount++;
                                var versionNumber = await CreateBpmnVersionFromImportAsync(created.Id, cleanedBpmn, sheetName, "auto_created");
                                versionedCount++;
                                laneReconcileQueue.Add((created.Id, cleanedBpmn));
                                results.Add(new { sheet = sheetName, file = bpmnFilePath, matchType = "Process", matchHow = "auto_created", matchCode = created.Code, matchName = created.NameAr, versioned = true, versionNumber, status = "success" });
                            }
                        }
                        else
                        {
                            // Log closest candidates across both tiers to help diagnose unmatched sheets.
                            var closeCandidates = processTasks
                                .Where(pt => !string.IsNullOrWhiteSpace(pt.NameAr) || !string.IsNullOrWhiteSpace(pt.NameEn))
                                .Select(pt => new
                                {
                                    pt.Code, pt.NameAr, pt.NameEn,
                                    Score = Math.Max(
                                        LevenshteinRatio(NormalizeForMatch(pt.NameAr), sheetNorm),
                                        LevenshteinRatio(NormalizeForMatch(pt.NameEn), sheetNorm))
                                })
                                .OrderByDescending(x => x.Score)
                                .Take(3)
                                .Select(x => $"[{x.Code}] {x.NameAr} / {x.NameEn} (score={x.Score:F2})")
                                .ToList();
                            _logger.LogWarning("Unmatched sheet: '{Sheet}' (norm='{Norm}'). Closest ProcessTasks: {Candidates}",
                                sheetName, sheetNorm, string.Join(" | ", closeCandidates));
                            results.Add(new { sheet = sheetName, file = bpmnFilePath, matchType = "file_only", status = "success" });
                        }
                    }

                    _logger.LogInformation("Converted: {Sheet} -> {File}", sheetName, bpmnFilePath);
                }
                catch (Exception ex)
                {
                    failCount++;
                    results.Add(new { sheet = sheetName, status = "error", error = ex.Message });
                    _logger.LogError(ex, "Error processing sheet: {Sheet}", sheetName);
                }
            }

            await _context.SaveChangesAsync();

            // Step 3: Lane reconciliation pass. Runs once, after every
            // Process row is saved with its final BpmnDiagram. For each
            // queued (processId, bpmnXml) pair: parse <bpmn:lane> nodes,
            // upsert BpmnLane rows, match each lane to an OrganizationUnit
            // by name (exact → normalized → autoCreate when enabled), and
            // back-fill Activity.OwningUnitId for flowNodeRefs inside the
            // lane. Logged per-Process so the operator sees which imports
            // had unreconciled lanes.
            var actorUserId = User?.Identity?.Name;
            var laneStats = new BpmnLaneReconcileResult(0, 0, 0, 0, 0, 0);
            foreach (var (procId, xml) in laneReconcileQueue)
            {
                try
                {
                    var r = await _bpmnLaneReconciler.ReconcileAsync(procId, xml, autoCreate, actorUserId);
                    laneStats = new BpmnLaneReconcileResult(
                        LanesSeen: laneStats.LanesSeen + r.LanesSeen,
                        Matched: laneStats.Matched + r.Matched,
                        AutoCreated: laneStats.AutoCreated + r.AutoCreated,
                        Unmatched: laneStats.Unmatched + r.Unmatched,
                        Orphaned: laneStats.Orphaned + r.Orphaned,
                        ActivitiesBackfilled: laneStats.ActivitiesBackfilled + r.ActivitiesBackfilled);
                    if (r.HasFindings)
                    {
                        _logger.LogInformation(
                            "Lane reconcile for {ProcessId}: seen={Seen} matched={Matched} autoCreated={Auto} unmatched={Unmatched} orphaned={Orphaned} activitiesBackfilled={Back}",
                            procId, r.LanesSeen, r.Matched, r.AutoCreated, r.Unmatched, r.Orphaned, r.ActivitiesBackfilled);
                    }
                }
                catch (Exception ex)
                {
                    // Non-fatal: lane reconcile failures should NOT break the
                    // import. Log + continue — the operator can re-run the
                    // reconcile from the admin UI.
                    _logger.LogWarning(ex, "Lane reconcile failed for process {ProcessId}; lanes remain unreconciled", procId);
                }
            }

            return Ok(new
            {
                success = true,
                message = $"Batch complete. Converted: {successCount}, Failed: {failCount}, DB matched: {dbMatchCount} (auto-created: {autoCreatedCount}), Versioned: {versionedCount}, Retry-recovered: {retrySucceededCount}, DI auto-fixes: {diRefRewriteCount}, Task-promotions: {taskPromotionCount}, Lanes: {laneStats.LanesSeen} ({laneStats.Matched} matched, {laneStats.AutoCreated} auto-created, {laneStats.Unmatched} unmatched), Activities backfilled: {laneStats.ActivitiesBackfilled}",
                totalSheets = sheetVisioMap.Count,
                batchStart = start,
                batchSize = batch.Count,
                lanes = new { laneStats.LanesSeen, laneStats.Matched, laneStats.AutoCreated, laneStats.Unmatched, laneStats.Orphaned, laneStats.ActivitiesBackfilled },
                successCount,
                failCount,
                dbMatchCount,
                versionedCount,
                autoCreatedCount,
                retrySucceededCount,
                diRefRewriteCount,
                taskPromotionCount,
                outputFolder,
                details = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Excel Visio to BPMN import");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
        finally
        {
            // Clean up temp file
            if (System.IO.File.Exists(tempPath))
                try { System.IO.File.Delete(tempPath); } catch { /* ignore */ }
        }
    }
}

/// <summary>
/// Visio to sheet name mapping
/// </summary>
public class VisioMapping
{
    public string SheetName { get; set; } = string.Empty;
    public string VisioFile { get; set; } = string.Empty;
}

// Data Transfer Objects for import

public class OrganizationImportData
{
    public List<BilingualItem> Sectors { get; set; } = new();
    public List<DepartmentItem> Departments { get; set; } = new();
    public List<SectionItem> Sections { get; set; } = new();
}

public class ProcessImportData
{
    public List<string> ProcessGroups { get; set; } = new();
    public List<BilingualItem> MainProcesses { get; set; } = new();
    public List<BilingualItem> SubProcesses { get; set; } = new();
    public List<BilingualItem> Procedures { get; set; } = new();
}

public class BilingualItem
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
}

public class DepartmentItem : BilingualItem
{
    public string Sector { get; set; } = string.Empty;
}

public class SectionItem : BilingualItem
{
    public string Department { get; set; } = string.Empty;
}

public class AnalysisOutput
{
    public List<BilingualItem> Sectors { get; set; } = new();
    public List<DepartmentItem> Departments { get; set; } = new();
    public List<SectionItem> Sections { get; set; } = new();
    public List<string> ProcessGroups { get; set; } = new();
    public List<BilingualItem> MainProcesses { get; set; } = new();
    public List<BilingualItem> SubProcesses { get; set; } = new();
    public List<BilingualItem> Procedures { get; set; } = new();
    public List<ProcedureDetailItem> ProcedureDetails { get; set; } = new();
    public List<string> DigitalSystems { get; set; } = new();
    public List<string> Services { get; set; } = new();
    public List<string> Partners { get; set; } = new();
    public List<string> Projects { get; set; } = new();
    public int TotalRecords { get; set; }
    public int TotalProcedures { get; set; }
}

/// <summary>
/// Detailed procedure record for Level 5 import
/// </summary>
public class ProcedureDetailItem
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public string? SubProcessEn { get; set; }
    public string? SubProcessAr { get; set; }
    public string? MainProcessEn { get; set; }
    public string? MainProcessAr { get; set; }
    public string? SectionEn { get; set; }
    public string? SectionAr { get; set; }
    public string? DepartmentEn { get; set; }
    public string? DepartmentAr { get; set; }
    public string? SectorEn { get; set; }
    public string? SectorAr { get; set; }
    public string? ProcessGroup { get; set; }
    public string? ProcedureStatus { get; set; }
    public string? CurrentProposed { get; set; }
    public string? AutomationStatus { get; set; }
    public string? DigitalSystem { get; set; }
    public string? Automable { get; set; }
    public string? AutomationScores { get; set; }
    public string? LinkedServices { get; set; }
    public string? ExternalPartners { get; set; }
    public string? Projects { get; set; }
    public string? DocumentReference { get; set; }
    public string? DocumentLanguage { get; set; }
}

