using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ESEMS.Web.Data;
using ESEMS.Web.Services.AI;
using ESEMS.Web.Services.AI.Prompts;
using ESEMS.Web.Services.Bpmn;
using ESEMS.Web.Services.Analysis;
using ESEMS.Web.Services.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for AI-powered features
/// </summary>
[Authorize(Policy = AppPolicies.Module.Ai.View)]
public class AIController : BaseController
{
    private readonly IAIService _aiService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AIController> _logger;
    private readonly IBpmnProcessingService _bpmnService;
    private readonly IProcessAnalysisService _analysisService;
    private readonly IMemoryCache _cache;
    private readonly IScopingService _scopingService;

    /// <summary>
    /// Cache TTL for AnalyzeX outputs. Long enough to absorb repeated
    /// clicks on the inline AI panel; short enough that a recent edit
    /// surfaces in the next view. Bypassed entirely when the cache key
    /// (which includes UpdatedAt.Ticks) changes after an edit.
    /// </summary>
    private static readonly TimeSpan AnalysisCacheTtl = TimeSpan.FromMinutes(30);

    public AIController(
        IAIService aiService,
        ApplicationDbContext context,
        ILogger<AIController> logger,
        IBpmnProcessingService bpmnService,
        IProcessAnalysisService analysisService,
        IMemoryCache cache,
        IScopingService scopingService)
    {
        _aiService = aiService;
        _context = context;
        _logger = logger;
        _bpmnService = bpmnService;
        _analysisService = analysisService;
        _cache = cache;
        _scopingService = scopingService;
    }

    /// <summary>
    /// Try-return cached AnalyzeX output; on miss, run <paramref name="produce"/>
    /// and cache for <see cref="AnalysisCacheTtl"/>. The key should include
    /// the entity's UpdatedAt timestamp so edits invalidate naturally.
    /// </summary>
    private async Task<(string analysis, bool cached)> GetOrCreateAnalysisAsync(string cacheKey, Func<Task<string>> produce)
    {
        if (_cache.TryGetValue<string>(cacheKey, out var hit) && !string.IsNullOrWhiteSpace(hit))
            return (hit, true);
        var fresh = await produce();
        if (!string.IsNullOrWhiteSpace(fresh))
            _cache.Set(cacheKey, fresh, AnalysisCacheTtl);
        return (fresh ?? string.Empty, false);
    }

    /// <summary>
    /// Returns a language instruction to append to AI analysis prompts
    /// </summary>
    private string GetLanguageInstruction()
    {
        var isArabic = CultureInfo.CurrentUICulture.Name.StartsWith("ar");
        return isArabic
            ? "\n\nIMPORTANT: You MUST respond ENTIRELY in Arabic (العربية). All headings, analysis, recommendations, and bullet points must be in Arabic."
            : "";
    }

    /// <summary>
    /// Returns an extra instruction to bias the analysis toward a specific
    /// focus area (efficiency, risk, automation, customer impact). Empty when
    /// no focus is supplied → full comprehensive analysis.
    /// </summary>
    private static string GetFocusInstruction(string? focus)
    {
        if (string.IsNullOrWhiteSpace(focus)) return string.Empty;
        return focus.ToLowerInvariant() switch
        {
            "efficiency" => "\n\nFOCUS: Prioritize efficiency, bottlenecks, cycle-time reduction, and lean-process thinking. Lead with the efficiency section and make it the longest.",
            "risk"       => "\n\nFOCUS: Prioritize risk exposure, control gaps, compliance (ISO 9001/20000), and incident-to-root-cause traceability. Lead with the risk section and make it the longest.",
            "automation" => "\n\nFOCUS: Prioritize automation candidates, manual-to-digital conversion, AI/RPA opportunities, and integration points. Rank candidates by ROI.",
            "customer"   => "\n\nFOCUS: Prioritize customer-impact, service-level risk, citizen-experience gaps, and touchpoint friction. Frame recommendations from the beneficiary's perspective.",
            _ => string.Empty
        };
    }

    /// <summary>
    /// AI Diagram Generator interface. Optionally accepts a `processId` query
    /// param to deep-link from a process page — the editor will pre-link the
    /// process and load its existing BPMN XML if any.
    /// </summary>
    public async Task<IActionResult> Diagrams(string? processId = null)
    {
        if (!string.IsNullOrWhiteSpace(processId))
        {
            var process = await _context.Processes
                .FirstOrDefaultAsync(p => p.Id == processId && !p.IsDeleted);
            if (process != null)
            {
                ViewBag.LinkedProcessId = process.Id;
                ViewBag.LinkedProcessName = process.NameEn;
                ViewBag.LinkedProcessNameAr = process.NameAr;
                ViewBag.LinkedProcessCode = process.Code;
                ViewBag.LinkedProcessXml = process.BpmnDiagram;
            }
        }
        return View();
    }

    /// <summary>
    /// AI Process Analyzer interface
    /// </summary>
    public async Task<IActionResult> ProcessAnalyzer()
    {
        // Pre-compute per-process counts so the UI can show context on dropdown
        // change without an extra round trip. Incidents/Problems are keyed by
        // ProcessId too.
        var incidentCounts = await _context.Incidents
            .Where(i => i.ProcessId != null)
            .GroupBy(i => i.ProcessId!)
            .Select(g => new { ProcessId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProcessId, x => x.Count);

        var problemCounts = await _context.Problems
            .Where(p => p.ProcessId != null)
            .GroupBy(p => p.ProcessId!)
            .Select(g => new { ProcessId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProcessId, x => x.Count);

        // Get Processes (L3 - Process level) with counts and BPMN presence
        var processesRaw = await _context.Processes
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Code)
            .Select(p => new
            {
                p.Id,
                p.Code,
                p.NameEn,
                p.NameAr,
                p.ProcessGroupId,
                // Null-safe projections — EF translates both ternary forms
                // to SQL COUNT subqueries, but this shape stays correct if
                // the query ever degrades to client-side evaluation.
                ActivityCount = p.Activities == null ? 0 : p.Activities.Count,
                RiskCount     = p.Risks == null      ? 0 : p.Risks.Count,
                HasBpmn       = p.BpmnDiagram != null && p.BpmnDiagram != ""
            })
            .ToListAsync();

        var processes = processesRaw.Select(p => new
        {
            p.Id,
            p.Code,
            p.NameEn,
            p.NameAr,
            p.ProcessGroupId,
            p.ActivityCount,
            p.RiskCount,
            IncidentCount = incidentCounts.TryGetValue(p.Id, out var ic) ? ic : 0,
            ProblemCount = problemCounts.TryGetValue(p.Id, out var pc) ? pc : 0,
            p.HasBpmn
        }).ToList();
        ViewBag.Processes = processes;

        // Get Process Groups (L2 - Department level) with rolled-up counts
        var groupsRaw = await _context.ProcessGroups
            .Where(pg => !pg.IsDeleted)
            .OrderBy(pg => pg.Code)
            .Select(pg => new
            {
                pg.Id,
                pg.Code,
                pg.NameEn,
                pg.NameAr,
                ProcessCount = pg.Processes!.Count(p => !p.IsDeleted)
            })
            .ToListAsync();

        var processGroups = groupsRaw.Select(g =>
        {
            var groupProcesses = processes.Where(p => p.ProcessGroupId == g.Id).ToList();
            return new
            {
                g.Id,
                g.Code,
                g.NameEn,
                g.NameAr,
                g.ProcessCount,
                ActivityCount = groupProcesses.Sum(p => p.ActivityCount),
                RiskCount = groupProcesses.Sum(p => p.RiskCount),
                IncidentCount = groupProcesses.Sum(p => p.IncidentCount),
                ProblemCount = groupProcesses.Sum(p => p.ProblemCount)
            };
        }).ToList();
        ViewBag.ProcessGroups = processGroups;

        return View();
    }

    // GetProcessBpmn moved to AIBpmnReadController.

    /// <summary>
    /// Analyze Process Group (L2 - Department level) using AI
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalyzeProcessGroup([FromBody] ProcessGroupAnalysisRequest request)
    {
        try
        {
            var processGroup = await _context.ProcessGroups
                .FirstOrDefaultAsync(pg => pg.Id == request.ProcessGroupId && !pg.IsDeleted);

            if (processGroup == null)
                return Json(new { success = false, error = "Process Group not found." });

            var ctx = await _analysisService.BuildProcessGroupAnalysisContextAsync(request.ProcessGroupId);

            // Plan: bilingual prompt built from the AnalyzerPrompts module.
            // Includes MBRHE system context, domain glossary, few-shot example,
            // the section structure, and the focus lens. Consistent between
            // Arabic and English so outputs can be compared 1:1.
            var isArabic = CultureInfo.CurrentUICulture.Name.StartsWith("ar");
            var analysisPrompt = AnalyzerPrompts.BuildProcessGroupAnalysisPrompt(
                ctx.ContextMarkdown, request.Focus, isArabic);

            var analysis = await _aiService.ChatAsync(analysisPrompt);

            return Json(new {
                success = true,
                analysis,
                processGroupName = CultureInfo.CurrentUICulture.Name.StartsWith("ar") ? processGroup.NameAr : processGroup.NameEn,
                processGroupCode = processGroup.Code,
                activityCount = ctx.ActivityCount,
                riskCount = ctx.RiskCount,
                incidentCount = ctx.IncidentCount,
                problemCount = ctx.ProblemCount,
                improvementCount = ctx.ImprovementCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing process group {ProcessGroupId}", request.ProcessGroupId);
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// Comprehensive process analysis using AI (L3 - Process level)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalyzeProcess([FromBody] ProcessAnalysisRequest request)
    {
        try
        {
            var process = await _context.Processes
                .FirstOrDefaultAsync(p => p.Id == request.ProcessId && !p.IsDeleted);

            if (process == null)
                return Json(new { success = false, error = "Process not found." });

            // F-019: record-level scope (IDOR). Don't return AI analysis of a
            // process outside the caller's org scope. 404 — don't leak existence.
            if (!(await _scopingService.GetScopeAsync(User)).CanAccess(process))
                return NotFound();

            var ctx = await _analysisService.BuildProcessAnalysisContextAsync(request.ProcessId);

            // Plan: bilingual prompt built from the AnalyzerPrompts module.
            // Includes MBRHE system context, domain glossary, few-shot example,
            // the six-section structure, and the focus lens.
            var isArabic = CultureInfo.CurrentUICulture.Name.StartsWith("ar");
            var analysisPrompt = AnalyzerPrompts.BuildProcessAnalysisPrompt(
                ctx.ContextMarkdown, request.Focus, isArabic);

            var analysis = await _aiService.ChatAsync(analysisPrompt);

            return Json(new {
                success = true,
                analysis,
                processName = CultureInfo.CurrentUICulture.Name.StartsWith("ar") ? process.NameAr : process.NameEn,
                processCode = process.Code,
                activityCount = ctx.ActivityCount,
                riskCount = ctx.RiskCount,
                incidentCount = ctx.IncidentCount,
                problemCount = ctx.ProblemCount,
                hasBpmn = ctx.HasBpmn
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing process {ProcessId}", request.ProcessId);
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// Structured Process Optimizer - returns categorized optimization suggestions with ROI
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OptimizeProcess([FromBody] ProcessAnalysisRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.ProcessId))
                return Json(new { success = false, error = "Process ID is required." });

            var process = await _context.Processes
                .Where(p => p.Id == request.ProcessId && !p.IsDeleted)
                .Select(p => new { p.Id, p.Code, Name = CultureInfo.CurrentUICulture.Name.StartsWith("ar") ? p.NameAr : p.NameEn })
                .FirstOrDefaultAsync();

            if (process == null)
                return Json(new { success = false, error = "Process not found." });

            var ctx = await _analysisService.BuildProcessAnalysisContextAsync(request.ProcessId);
            var isArabic = CultureInfo.CurrentUICulture.Name.StartsWith("ar");
            var lang = isArabic ? "Arabic" : "English";

            var prompt = $@"You are a process optimization expert for MBRHE (Mohammed Bin Rashid Housing Establishment), a Dubai government housing entity.
Analyze the following process data and provide specific, actionable optimization suggestions.

{ctx.ContextMarkdown}

Respond in {lang} with EXACTLY this JSON structure. Do NOT wrap in markdown code fences.
Return ONLY valid JSON:
{{
  ""quickWins"": [
    {{ ""title"": ""..."", ""description"": ""..."", ""estimatedCostSavings"": 0, ""estimatedTimeSavingsHours"": 0, ""effortLevel"": ""Low"" }}
  ],
  ""mediumTerm"": [
    {{ ""title"": ""..."", ""description"": ""..."", ""estimatedCostSavings"": 0, ""estimatedTimeSavingsHours"": 0, ""effortLevel"": ""Medium"" }}
  ],
  ""longTerm"": [
    {{ ""title"": ""..."", ""description"": ""..."", ""estimatedCostSavings"": 0, ""estimatedTimeSavingsHours"": 0, ""effortLevel"": ""High"" }}
  ],
  ""roiSummary"": {{
    ""totalEstimatedCostSavings"": 0,
    ""totalEstimatedTimeSavingsHours"": 0,
    ""implementationCostEstimate"": 0,
    ""paybackPeriodMonths"": 0
  }}
}}

Guidelines:
- Quick Wins: 2-3 items achievable in < 1 month with low effort
- Medium-Term: 2-3 items requiring 1-3 months
- Long-Term: 1-2 items for 3+ months with high strategic impact
- Cost savings in AED, time savings in hours per year
- Be specific to this process — reference actual activities, roles, and data from the context";

            var response = await _aiService.ChatAsync(prompt);

            // Try to parse as JSON
            object? parsed = null;
            try
            {
                // Strip markdown fences if present
                var jsonStr = response.Trim();
                if (jsonStr.StartsWith("```"))
                {
                    jsonStr = Regex.Replace(jsonStr, @"^```\w*\s*", "");
                    jsonStr = Regex.Replace(jsonStr, @"\s*```\s*$", "");
                }
                parsed = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(jsonStr);
            }
            catch
            {
                // Fallback: return as raw text
                parsed = null;
            }

            return Json(new
            {
                success = true,
                optimization = parsed ?? (object)response,
                processName = process.Name,
                processCode = process.Code
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing process {ProcessId}", request.ProcessId);
            return Json(new { success = false, error = "An error occurred while optimizing the process." });
        }
    }

    /// <summary>
    /// Generate process improvement suggestions
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateProcessImprovements(string processId)
    {
        try
        {
            var process = await _context.Processes
                .FirstOrDefaultAsync(p => p.Id == processId && !p.IsDeleted);

            if (process == null)
                return NotFound();

            var suggestions = await _aiService.GenerateProcessImprovementSuggestionsAsync(
                process.Name,
                process.Description ?? "",
                null
            );

            return Json(new { success = true, suggestions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating process improvements");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// Analyze risk and suggest mitigation
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalyzeRisk(string riskId)
    {
        try
        {
            var risk = await _context.ProcessRisks
                .FirstOrDefaultAsync(r => r.Id == riskId);

            if (risk == null)
                return NotFound();

            var analysis = await _aiService.AnalyzeRiskAndSuggestMitigationAsync(
                risk.Description ?? "",
                risk.Category ?? "General",
                risk.LikelihoodScore,
                risk.ImpactScore
            );

            return Json(new { success = true, analysis });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing risk");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// Analyze enterprise risk and suggest mitigation
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalyzeEnterpriseRisk(string riskId)
    {
        try
        {
            var risk = await _context.EnterpriseRisks
                .Include(r => r.Category)
                .FirstOrDefaultAsync(r => r.Id == riskId && !r.IsDeleted);

            if (risk == null)
                return NotFound();

            // F-019: record-level scope (IDOR) — don't analyze an out-of-scope risk.
            if (!(await _scopingService.GetScopeAsync(User)).CanAccess(risk))
                return NotFound();

            var culture = CultureInfo.CurrentUICulture.Name.StartsWith("ar") ? "ar" : "en";
            var cacheKey = $"ai:risk:{riskId}:{risk.UpdatedAt.Ticks}:{culture}";

            var (analysis, cached) = await GetOrCreateAnalysisAsync(cacheKey, async () =>
            {
                var riskDesc = risk.GetLocalizedDescription() ?? risk.GetLocalizedName();
                var catName = culture == "ar" ? (risk.Category?.NameAr ?? risk.Category?.NameEn ?? "عام") : (risk.Category?.NameEn ?? "General");
                var raw = await _aiService.AnalyzeRiskAndSuggestMitigationAsync(riskDesc, catName, risk.Likelihood, risk.Impact);

                // Arabic post-translation pass — same as before, just inside the producer.
                if (culture == "ar")
                {
                    raw = await _aiService.ChatAsync(
                        $"Translate the following risk analysis to Arabic while keeping the structure and formatting:\n\n{raw}\n\nRespond ENTIRELY in Arabic.",
                        new List<(string, string)> { ("system", "You are a translator. Translate the full content to Arabic preserving all markdown formatting.") });
                }
                return raw;
            });

            return Json(new { success = true, analysis, cached });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing enterprise risk");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// Summarize audit logs
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SummarizeAuditLogs([FromBody] List<string> logs)
    {
        try
        {
            if (logs == null || !logs.Any())
                return Json(new { success = false, error = "No logs provided." });

            var summary = await _aiService.SummarizeAuditLogsAsync(logs, "Recent Activity");
            return Json(new { success = true, summary });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error summarizing audit logs");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// Generate RACI suggestions for activity
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateRACISuggestions(string activityId)
    {
        try
        {
            var activity = await _context.Activities
                .Include(a => a.Process)
                .FirstOrDefaultAsync(a => a.Id == activityId && !a.IsDeleted);

            if (activity == null)
                return NotFound();

            var suggestions = await _aiService.GenerateRACISuggestionsAsync(
                activity.Name,
                activity.Description ?? "",
                $"Process: {activity.Process?.Name}"
            );

            return Json(new { success = true, suggestions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating RACI suggestions");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// Analyze service performance
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalyzeServicePerformance(string serviceId)
    {
        try
        {
            var service = await _context.Services
                .FirstOrDefaultAsync(s => s.Id == serviceId && !s.IsDeleted);

            if (service == null)
                return NotFound();

            // F-019: record-level scope (IDOR) — don't analyze an out-of-scope service.
            if (!(await _scopingService.GetScopeAsync(User)).CanAccess(service))
                return NotFound();

            var analysis = await _aiService.AnalyzeServicePerformanceAsync(
                service.Name,
                service.CustomerSatisfactionScore,
                service.AnnualTransactionCount,
                null
            );

            return Json(new { success = true, analysis });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing service performance");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalyzeIncident(string incidentId)
    {
        try
        {
            var incident = await _context.Incidents.FirstOrDefaultAsync(i => i.Id == incidentId && !i.IsDeleted);
            if (incident == null) return NotFound();
            // F-019: record-level scope (IDOR) — don't analyze an out-of-scope incident.
            if (!(await _scopingService.GetScopeAsync(User)).CanAccess(incident)) return NotFound();
            var name = incident.GetLocalizedName(); var desc = incident.GetLocalizedDescription();
            var prompt = $"Analyze this incident: {name} - {desc}. Priority: {incident.Priority}. Provide 1) Immediate troubleshooting steps, 2) Potential root causes, and 3) Preventive measures. Format in clean markdown without fences.{GetLanguageInstruction()}";
            var analysis = await _aiService.ChatAsync(prompt, new List<(string, string)> { ("system", "You are an ITSM expert for MBRHE.") });
            return Json(new { success = true, analysis });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error analyzing incident"); return Json(new { success = false, error = "Error analyzing incident." }); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalyzeProblem(string problemId)
    {
        try
        {
            var problem = await _context.Problems.FirstOrDefaultAsync(p => p.Id == problemId && !p.IsDeleted);
            if (problem == null) return NotFound();
            var name = problem.GetLocalizedName(); var desc = problem.GetLocalizedDescription();
            var prompt = $"Perform a Root Cause Analysis (RCA) or 5 Whys on this problem: {name} - {desc}. Suggest permanent fixes and workarounds. Format in clean markdown without fences.{GetLanguageInstruction()}";
            var analysis = await _aiService.ChatAsync(prompt, new List<(string, string)> { ("system", "You are a Problem Management expert for MBRHE.") });
            return Json(new { success = true, analysis });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error analyzing problem"); return Json(new { success = false, error = "Error analyzing problem." }); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalyzeChangeRequest(string changeRequestId)
    {
        try
        {
            var change = await _context.ChangeRequests.FirstOrDefaultAsync(c => c.Id == changeRequestId && !c.IsDeleted);
            if (change == null) return NotFound();
            var culture = CultureInfo.CurrentUICulture.Name.StartsWith("ar") ? "ar" : "en";
            var cacheKey = $"ai:cr:{changeRequestId}:{change.UpdatedAt.Ticks}:{culture}";
            var (analysis, cached) = await GetOrCreateAnalysisAsync(cacheKey, async () =>
            {
                var name = change.GetLocalizedName(); var desc = change.GetLocalizedDescription();
                var prompt = $"Analyze this change request: Title: {name}. Description: {desc}. Reason: {change.Justification}. Provide: 1) Risk impact assessment, 2) Required pre-requisites, 3) Rollback considerations. Format in clean markdown without fences.{GetLanguageInstruction()}";
                return await _aiService.ChatAsync(prompt, new List<(string, string)> { ("system", "You are a Change Advisory Board expert for MBRHE.") });
            });
            return Json(new { success = true, analysis, cached });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error analyzing change request"); return Json(new { success = false, error = "Error analyzing change request." }); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalyzeCustomerFeedback(string feedbackId)
    {
        try
        {
            var feedback = await _context.CustomerFeedbacks.FirstOrDefaultAsync(f => f.Id == feedbackId && !f.IsDeleted);
            if (feedback == null) return NotFound();
            var desc = feedback.GetLocalizedDescription();
            var prompt = $"Analyze this customer feedback: '{desc}'. Provide: 1) Sentiment analysis, 2) Key issues/highlights, and 3) Draft a professional response. Format in clean markdown without fences.{GetLanguageInstruction()}";
            var analysis = await _aiService.ChatAsync(prompt, new List<(string, string)> { ("system", "You are a Customer Experience expert for MBRHE.") });
            return Json(new { success = true, analysis });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error analyzing feedback"); return Json(new { success = false, error = "Error analyzing feedback." }); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalyzeImprovement(string improvementId)
    {
        try
        {
            var improvement = await _context.ImprovementInitiatives.FirstOrDefaultAsync(i => i.Id == improvementId && !i.IsDeleted);
            if (improvement == null) return NotFound();
            // F-019: record-level scope (IDOR) — don't analyze an out-of-scope initiative.
            if (!(await _scopingService.GetScopeAsync(User)).CanAccess(improvement)) return NotFound();
            var culture = CultureInfo.CurrentUICulture.Name.StartsWith("ar") ? "ar" : "en";
            var cacheKey = $"ai:imp:{improvementId}:{improvement.UpdatedAt.Ticks}:{improvement.ImpactScore}:{improvement.EffortScore}:{culture}";
            var (analysis, cached) = await GetOrCreateAnalysisAsync(cacheKey, async () =>
            {
                var name = improvement.GetLocalizedName(); var desc = improvement.GetLocalizedDescription();
                var prompt = $"Analyze this improvement initiative: {name} - {desc}. Impact Score: {improvement.ImpactScore}, Effort Score: {improvement.EffortScore}. Provide: 1) ROI evaluation, 2) Execution risks, 3) Implementation steps. Format in clean markdown without fences.{GetLanguageInstruction()}";
                return await _aiService.ChatAsync(prompt, new List<(string, string)> { ("system", "You are a Continuous Improvement expert for MBRHE.") });
            });
            return Json(new { success = true, analysis, cached });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error analyzing improvement"); return Json(new { success = false, error = "Error analyzing improvement." }); }
    }

    public class InitiativeScoringRequest
    {
        public string? TitleEn { get; set; }
        public string? TitleAr { get; set; }
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public string? ProcessName { get; set; }
        public string? Scope { get; set; }
    }

    /// <summary>
    /// Returns suggested Impact/Effort scores for an improvement initiative.
    /// Used by the Wizard to pre-fill the sliders on the scoring step so the
    /// PMO can confirm or adjust instead of starting from neutral 5/5.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuggestInitiativeScoring([FromBody] InitiativeScoringRequest request)
    {
        try
        {
            if (request == null || (string.IsNullOrWhiteSpace(request.TitleEn) && string.IsNullOrWhiteSpace(request.TitleAr)))
                return Json(new { success = false, error = "Title is required." });

            var suggestion = await _aiService.SuggestInitiativeScoringAsync(
                request.TitleEn ?? "",
                request.TitleAr ?? "",
                request.DescriptionEn,
                request.DescriptionAr,
                request.ProcessName,
                request.Scope
            );

            return Json(new
            {
                success = true,
                impact = suggestion.Impact,
                effort = suggestion.Effort,
                quadrant = suggestion.Quadrant,
                reasoning = suggestion.Reasoning
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suggesting initiative scoring");
            return Json(new { success = false, error = "Error generating scoring suggestion." });
        }
    }

    /// <summary>
    /// Generate BPMN diagram using AI
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateBPMN([FromBody] BPMNRequest request)
    {
        try
        {
			const int maxAttempts = 2;
			string lastRaw = "";

			for (int attempt = 1; attempt <= maxAttempts; attempt++)
			{
				var bpmnXml = await _aiService.GenerateBPMNDiagramAsync(
					request.Title ?? "Process",
					request.Description ?? "",
					request.Steps
				);
				lastRaw = bpmnXml;

				var cleaned = _bpmnService.CleanBpmnXml(bpmnXml);

				// Debug: log a sample of name= attributes before & after cleaning
				var rawNameSample = System.Text.RegularExpressions.Regex.Match(bpmnXml ?? "", @"name=""([^""]{1,80})""");
				var cleanNameSample = System.Text.RegularExpressions.Regex.Match(cleaned ?? "", @"name=""([^""]{1,80})""");
				_logger.LogWarning("BPMN DEBUG RAW name: {Raw}", rawNameSample.Success ? rawNameSample.Groups[1].Value : "(none)");
				_logger.LogWarning("BPMN DEBUG CLEAN name: {Clean}", cleanNameSample.Success ? cleanNameSample.Groups[1].Value : "(none)");

				if (string.IsNullOrWhiteSpace(cleaned) || cleaned.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
				{
					if (attempt < maxAttempts)
					{
						_logger.LogWarning("BPMN generation attempt {Attempt} returned empty/error, retrying...", attempt);
						continue;
					}
					return Json(new { success = false, error = string.IsNullOrWhiteSpace(cleaned) ? Localizer["Error_EmptyResponse"].Value : cleaned });
				}

				if (!_bpmnService.LooksLikeBpmnXml(cleaned))
				{
					if (attempt < maxAttempts)
					{
						_logger.LogWarning("BPMN generation attempt {Attempt} returned invalid XML, retrying...", attempt);
						continue;
					}
					return Json(new { success = false, error = "AI did not return valid BPMN XML.", raw = lastRaw });
				}

				return Json(new { success = true, bpmnXml = cleaned });
			}

			return Json(new { success = false, error = "Failed to generate valid BPMN after multiple attempts.", raw = lastRaw });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating BPMN diagram");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// Refine/update an existing BPMN diagram
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefineBPMN([FromBody] RefineRequest request)
    {
        try
        {
			var currentXml = !string.IsNullOrWhiteSpace(request.CurrentBpmnXml)
				? request.CurrentBpmnXml!
				: (request.CurrentMermaidCode ?? string.Empty);

			if (string.IsNullOrWhiteSpace(currentXml))
                return Json(new { success = false, error = "No existing diagram to refine." });

            if (string.IsNullOrWhiteSpace(request.Instructions))
                return Json(new { success = false, error = "Please provide update instructions." });

			var bpmnXml = await _aiService.RefineBPMNDiagramAsync(
				currentXml,
                request.Instructions
            );

			var cleaned = _bpmnService.CleanBpmnXml(bpmnXml);
            if (string.IsNullOrWhiteSpace(cleaned))
                return Json(new { success = false, error = Localizer["Error_EmptyResponse"].Value });

            if (cleaned.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, error = cleaned });

			if (!_bpmnService.LooksLikeBpmnXml(cleaned))
				return Json(new { success = false, error = Localizer["Error_InvalidMermaidCode"].Value, raw = bpmnXml });

			return Json(new { success = true, bpmnXml = cleaned });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refining BPMN diagram");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// Optimize/refine user prompt for better AI results
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OptimizePrompt([FromBody] OptimizePromptRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return Json(new { success = false, error = "Please provide a prompt to optimize." });

            var isArabic = request.IsArabic || CultureInfo.CurrentUICulture.Name.StartsWith("ar");
            var systemPrompt = _analysisService.BuildOptimizedBpmnPrompt(isArabic);

            var userPrompt = isArabic
                ? $@"الوصف الأصلي من المستخدم:
""{request.Prompt}""

قم بتحسين هذا الوصف لتوليد مخطط BPMN أفضل وأكثر تفصيلاً. اجعله أكثر دقة وتحديداً مع الحفاظ على الهدف الأساسي. يجب أن يكون الرد باللغة العربية فقط."
                : $@"Original prompt from user:
""{request.Prompt}""

Please optimize this prompt to generate a better BPMN diagram. Make it more detailed and specific while keeping the core intent.";

            var optimizedPrompt = await _aiService.ChatAsync(userPrompt, new List<(string role, string content)>
            {
                ("system", systemPrompt)
            });

            optimizedPrompt = _analysisService.CleanOptimizedPromptResponse(optimizedPrompt);

            return Json(new { success = true, optimizedPrompt });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing prompt");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// Import Visio file and convert to BPMN
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportVisio(IFormFile file, string? context = null)
    {
        try
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, error = "No file uploaded." });

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".vsdx" && extension != ".vsd" && extension != ".xml")
                return Json(new { success = false, error = "Please upload a Visio file (.vsdx, .vsd) or XML export." });

            string xmlContent;

            if (extension == ".vsdx")
            {
                // VSDX is a ZIP containing XML files
                using var stream = file.OpenReadStream();
                using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);

                // Try to find the main page XML
                var pageEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.Contains("pages/page", StringComparison.OrdinalIgnoreCase) &&
                    e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

                if (pageEntry == null)
                    pageEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

                if (pageEntry == null)
                    return Json(new { success = false, error = "Could not find page content in Visio file." });

                using var entryStream = pageEntry.Open();
                using var reader = new StreamReader(entryStream);
                xmlContent = await reader.ReadToEndAsync();
            }
            else
            {
                // Direct XML or older VSD format
                using var reader = new StreamReader(file.OpenReadStream());
                xmlContent = await reader.ReadToEndAsync();
            }

			var bpmnXml = await _aiService.ConvertVisioToBPMNAsync(xmlContent, context);

			var cleaned = _bpmnService.CleanBpmnXml(bpmnXml);
            if (string.IsNullOrWhiteSpace(cleaned))
                return Json(new { success = false, error = Localizer["Error_VisioConversion"].Value });

            if (cleaned.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, error = cleaned });

			if (!_bpmnService.LooksLikeBpmnXml(cleaned))
				return Json(new { success = false, error = Localizer["Error_InvalidVisioFile"].Value, raw = bpmnXml });

			return Json(new { success = true, bpmnXml = cleaned });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Visio file");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// RAG AI Assistant - context-aware Q&A
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AskAssistant([FromBody] AssistantRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                return Json(new { success = false, error = "Please provide a question." });

            var assistantService = HttpContext.RequestServices.GetRequiredService<ESEMS.Web.Services.AI.AiAssistantService>();
            // Pass the current UI culture so the assistant replies in the user's language
            var response = await assistantService.AskAsync(
                request.Question,
                request.ConversationHistory?.Select(m => (m.Role, m.Content)).ToList(),
                culture: System.Globalization.CultureInfo.CurrentUICulture.Name);

            // FEAT-5: deep-link awareness — scan the answer for entity codes
            // (RSK / CR / SVC / AST / IMP) and rewrite them as markdown links
            // so the chat client renders clickable entries.
            var (linkedAnswer, links) = await ResolveEntityLinksInAnswerAsync(response.Answer ?? "");

            return Json(new
            {
                success = true,
                answer = linkedAnswer,
                suggestions = response.Suggestions,
                sourceCount = response.SourceCount,
                entityLinks = links
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AI Assistant");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// Scans an assistant answer for entity codes, looks them up in the DB,
    /// and rewrites the first plain-text occurrence of each into a markdown
    /// link. Skips codes already inside a markdown link (negative lookbehind)
    /// so the assistant can emit pre-linked references too without doubling.
    /// Returns the rewritten answer plus a deduped list of detected links
    /// (so the client can render a "Referenced records" chip strip).
    /// </summary>
    private async Task<(string Answer, List<EntityLink> Links)> ResolveEntityLinksInAnswerAsync(string answer)
    {
        var links = new List<EntityLink>();
        if (string.IsNullOrWhiteSpace(answer)) return (answer, links);

        // Distinct codes per entity type. Lookbehind protects codes that the
        // model already wrapped in [text](url) syntax.
        var rskCodes = Regex.Matches(answer, @"(?<![\[\(\w])RSK-\d{4}-\d{3,}\b")
            .Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var crCodes  = Regex.Matches(answer, @"(?<![\[\(\w])CR-\d{3,}\b")
            .Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var svcCodes = Regex.Matches(answer, @"(?<![\[\(\w])SVC-\d{3,}\b")
            .Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var astCodes = Regex.Matches(answer, @"(?<![\[\(\w])AST-\d{4}-\d{3,}\b")
            .Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var impCodes = Regex.Matches(answer, @"(?<![\[\(\w])IMP-\d{3,}\b")
            .Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (rskCodes.Count == 0 && crCodes.Count == 0 && svcCodes.Count == 0 && astCodes.Count == 0 && impCodes.Count == 0)
            return (answer, links);

        // Batched DB resolution — one round-trip per entity type, only when codes were found.
        var resolved = new Dictionary<string, (string Url, string Label)>(StringComparer.OrdinalIgnoreCase);

        if (rskCodes.Count > 0)
        {
            var rows = await _context.EnterpriseRisks
                .Where(r => rskCodes.Contains(r.RiskNumber) && !r.IsDeleted)
                .Select(r => new { r.RiskNumber, r.Id, r.NameEn, r.NameAr }).ToListAsync();
            foreach (var r in rows) resolved[r.RiskNumber] = ($"/EnterpriseRisks/Details/{r.Id}", r.NameEn ?? r.NameAr ?? r.RiskNumber);
        }
        if (crCodes.Count > 0)
        {
            var rows = await _context.ChangeRequests
                .Where(c => crCodes.Contains(c.Code) && !c.IsDeleted)
                .Select(c => new { c.Code, c.Id, c.NameEn, c.NameAr }).ToListAsync();
            foreach (var c in rows) resolved[c.Code] = ($"/ChangeRequests/Details/{c.Id}", c.NameEn ?? c.NameAr ?? c.Code);
        }
        if (svcCodes.Count > 0)
        {
            var rows = await _context.Services
                .Where(s => svcCodes.Contains(s.Code) && !s.IsDeleted)
                .Select(s => new { s.Code, s.Id, s.NameEn, s.NameAr }).ToListAsync();
            foreach (var s in rows) resolved[s.Code] = ($"/Services/Details/{s.Id}", s.NameEn ?? s.NameAr ?? s.Code);
        }
        if (astCodes.Count > 0)
        {
            var rows = await _context.Assets
                .Where(a => astCodes.Contains(a.AssetTag) && !a.IsDeleted)
                .Select(a => new { a.AssetTag, a.Id, a.NameEn, a.NameAr }).ToListAsync();
            foreach (var a in rows) resolved[a.AssetTag] = ($"/Assets/Details/{a.Id}", a.NameEn ?? a.NameAr ?? a.AssetTag);
        }
        if (impCodes.Count > 0)
        {
            var rows = await _context.ImprovementInitiatives
                .Where(i => impCodes.Contains(i.Code) && !i.IsDeleted)
                .Select(i => new { i.Code, i.Id, i.TitleEn, i.TitleAr }).ToListAsync();
            foreach (var i in rows) resolved[i.Code] = ($"/Improvements/Details/{i.Id}", i.TitleEn ?? i.TitleAr ?? i.Code);
        }

        if (resolved.Count == 0) return (answer, links);

        // Single pass: longest codes first so AST-2025-0001 isn't shadowed by a partial 2025-0001 match.
        foreach (var kv in resolved.OrderByDescending(kv => kv.Key.Length))
        {
            var code = kv.Key;
            var (url, label) = kv.Value;
            var pattern = $@"(?<![\[\(\w]){Regex.Escape(code)}\b";
            answer = Regex.Replace(answer, pattern, $"[{code}]({url} \"{label.Replace("\"", "'")}\")", RegexOptions.IgnoreCase);
            links.Add(new EntityLink { Code = code, Url = url, Label = label });
        }

        return (answer, links);
    }

    public class EntityLink
    {
        public string Code { get; set; } = "";
        public string Url { get; set; } = "";
        public string Label { get; set; } = "";
    }

    // GetProcesses / GetProcessesWithBPMN / LoadProcessBPMN /
    // GetProcessTasksWithBPMN / LoadProcessTaskBPMN moved to
    // AIBpmnReadController. Their URLs (/AI/Get…) are preserved via
    // attribute routing on the new controller.

    /// <summary>
    /// Save BPMN diagram to a process
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    public async Task<IActionResult> SaveBPMNToProcess([FromBody] SaveBPMNRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.BpmnXml))
                return Json(new { success = false, error = "No BPMN diagram to save." });

            Models.APQC.Process? process;
            bool isNewProcess = false;

            if (!string.IsNullOrWhiteSpace(request.ProcessId))
            {
                // Update existing process
                process = await _context.Processes
                    .FirstOrDefaultAsync(p => p.Id == request.ProcessId && !p.IsDeleted);

                if (process == null)
                    return Json(new { success = false, error = "Process not found." });

                // F-007: record-level scope (IDOR) on BPMN write. Mirror
                // ProcessesController.UpdateBpmnDiagram — a scoped user must not
                // overwrite the BPMN of a process outside their org subtree.
                if (!(await _scopingService.GetScopeAsync(User)).CanAccess(process))
                    return Json(new { success = false, error = "Process not found." });

                // Create version record if BPMN already exists
                await CreateBpmnVersionRecord(process.Id, request.BpmnXml, request.ChangeDescription);

                process.BpmnDiagram = request.BpmnXml;
                process.UpdatedAt = DateTime.UtcNow;
            }
            else if (!string.IsNullOrWhiteSpace(request.ProcessName))
            {
                // Create new process
                var processGroup = await _context.ProcessGroups
                    .Where(pg => !pg.IsDeleted)
                    .OrderBy(pg => pg.Code)
                    .FirstOrDefaultAsync();

                if (processGroup == null)
                    return Json(new { success = false, error = "No process group found. Please create a process group first." });

                process = new Models.APQC.Process
                {
                    Id = Guid.NewGuid().ToString(),
                    NameEn = request.ProcessName,
                    NameAr = request.ProcessName,
                    Code = $"BPMN-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    ProcessGroupId = processGroup.Id,
                    BpmnDiagram = request.BpmnXml,
                    Status = Models.Enums.ProcessStatus.Draft,
                    ProcessType = Models.Enums.ProcessType.Core,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Processes.Add(process);
                isNewProcess = true;
            }
            else
            {
                return Json(new { success = false, error = "Please provide either a process ID or a process name." });
            }

            await _context.SaveChangesAsync();

            // Create initial version for new process
            if (isNewProcess)
            {
                await CreateBpmnVersionRecord(process.Id, request.BpmnXml, "Initial version");
                await _context.SaveChangesAsync();
            }

            var message = CultureInfo.CurrentUICulture.Name.StartsWith("ar")
                ? "تم حفظ المخطط بنجاح!"
                : "BPMN diagram saved successfully!";

            return Json(new
            {
                success = true,
                message,
                processId = process.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving BPMN to process");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// Create a BPMN version record
    /// </summary>
    private async Task CreateBpmnVersionRecord(string processId, string bpmnXml, string? changeDescription)
    {
        // Mark all existing versions as not current
        var existingVersions = await _context.ProcessBpmnVersions
            .Where(v => v.ProcessId == processId)
            .ToListAsync();

        foreach (var version in existingVersions)
        {
            version.IsCurrent = false;
        }

        // Get next version number
        var nextVersionNumber = existingVersions.Any()
            ? existingVersions.Max(v => v.VersionNumber) + 1
            : 1;

        // Get current user info
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userName = User.Identity?.Name ?? "System";

        // Create new version record
        var versionRecord = new Models.APQC.ProcessBpmnVersion
        {
            Id = Guid.NewGuid().ToString(),
            ProcessId = processId,
            VersionNumber = nextVersionNumber,
            BpmnXml = bpmnXml,
            ChangeDescription = changeDescription ?? $"Version {nextVersionNumber}",
            CreatedById = userId,
            CreatedByName = userName,
            CreatedAt = DateTime.UtcNow,
            IsCurrent = true,
            XmlSizeBytes = System.Text.Encoding.UTF8.GetByteCount(bpmnXml)
        };

        _context.ProcessBpmnVersions.Add(versionRecord);
    }

    /// <summary>
    /// Import BPMN files from ConvertedBPMN directory, match to processes, and save
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.Module.Process.Edit)]
    public async Task<IActionResult> ImportBpmnFromFiles([FromBody] BatchBpmnImportRequest request)
    {
        try
        {
            // Locate the ConvertedBPMN directory
            var contentRoot = Path.GetDirectoryName(Directory.GetCurrentDirectory()) ?? Directory.GetCurrentDirectory();
            var bpmnDir = Path.Combine(contentRoot, "ConvertedBPMN");

            // Try common paths if not found
            if (!Directory.Exists(bpmnDir))
            {
                var altPaths = new[]
                {
                    @"C:\Users\kalmi\OneDrive\Desktop\desktop 2\MB1\ConvertedBPMN",
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "ConvertedBPMN"),
                    Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "ConvertedBPMN")),
                };
                bpmnDir = altPaths.FirstOrDefault(Directory.Exists) ?? bpmnDir;
            }

            if (!Directory.Exists(bpmnDir))
                return Json(new { success = false, error = $"ConvertedBPMN directory not found. Tried: {bpmnDir}" });

            var bpmnFiles = Directory.GetFiles(bpmnDir, "*.bpmn").OrderBy(f => f).ToList();
            if (!bpmnFiles.Any())
                return Json(new { success = false, error = "No .bpmn files found in directory." });

            // Load all processes from DB
            var allProcesses = await _context.Processes
                .Where(p => !p.IsDeleted)
                .Select(p => new { p.Id, p.NameAr, p.NameEn, p.Code })
                .ToListAsync();

            // Load analysis_output.json for procedure→subprocess mapping
            var bpmnParent = Path.GetDirectoryName(bpmnDir) ?? bpmnDir;
            var analysisPath = Path.Combine(bpmnParent, "ExcelReader", "analysis_output.json");
            if (!System.IO.File.Exists(analysisPath))
            {
                analysisPath = @"C:\Users\kalmi\OneDrive\Desktop\desktop 2\MB1\ExcelReader\analysis_output.json";
            }
            List<ProcedureMapping>? procedureMappings = null;
            if (System.IO.File.Exists(analysisPath))
            {
                var analysisJson = await System.IO.File.ReadAllTextAsync(analysisPath);
                var analysisData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(analysisJson);
                if (analysisData.TryGetProperty("ProcedureDetails", out var procedures))
                {
                    procedureMappings = new List<ProcedureMapping>();
                    foreach (var proc in procedures.EnumerateArray())
                    {
                        procedureMappings.Add(new ProcedureMapping
                        {
                            NameAr = proc.GetProperty("NameAr").GetString() ?? "",
                            NameEn = proc.GetProperty("NameEn").GetString() ?? "",
                            SubProcessAr = proc.TryGetProperty("SubProcessAr", out var sp) ? sp.GetString() ?? "" : "",
                            SubProcessEn = proc.TryGetProperty("SubProcessEn", out var spe) ? spe.GetString() ?? "" : "",
                            MainProcessAr = proc.TryGetProperty("MainProcessAr", out var mp) ? mp.GetString() ?? "" : "",
                            MainProcessEn = proc.TryGetProperty("MainProcessEn", out var mpe) ? mpe.GetString() ?? "" : "",
                        });
                    }
                }
            }

            // Build mapping: extract first task name from each BPMN and match
            var mappings = new List<object>();
            var matched = new List<(string filePath, string processId, string bpmnXml, string matchedName)>();
            var unmatched = new List<object>();

            foreach (var file in bpmnFiles)
            {
                var xml = await System.IO.File.ReadAllTextAsync(file);
                var fileName = Path.GetFileNameWithoutExtension(file);

                // Extract procedure name from first bpmn:task
                var taskMatch = Regex.Match(xml, @"<bpmn:task[^>]*name=""([^""]+)""", RegexOptions.None);
                var procedureName = taskMatch.Success ? taskMatch.Groups[1].Value.Trim() : null;

                if (string.IsNullOrEmpty(procedureName))
                {
                    unmatched.Add(new { file = fileName, reason = "No task name found in BPMN" });
                    continue;
                }

                // Strategy 1: Use analysis_output.json to find procedure → subprocess → process
                string? matchedProcessId = null;
                string? matchedProcessName = null;

                if (procedureMappings != null)
                {
                    // Find this procedure in analysis data
                    var procInfo = procedureMappings.FirstOrDefault(p =>
                        NormalizeArabic(p.NameAr) == NormalizeArabic(procedureName));

                    // Fallback: contains match
                    if (procInfo == null)
                    {
                        procInfo = procedureMappings.FirstOrDefault(p =>
                            !string.IsNullOrEmpty(p.NameAr) &&
                            (NormalizeArabic(p.NameAr).Contains(NormalizeArabic(procedureName)) ||
                             NormalizeArabic(procedureName).Contains(NormalizeArabic(p.NameAr))));
                    }

                    if (procInfo != null)
                    {
                        // Try matching SubProcess name to DB Process name
                        var dbProcess = allProcesses.FirstOrDefault(p =>
                            !string.IsNullOrEmpty(p.NameAr) &&
                            NormalizeArabic(p.NameAr) == NormalizeArabic(procInfo.SubProcessAr));

                        // Fallback: contains match on subprocess
                        if (dbProcess == null && !string.IsNullOrEmpty(procInfo.SubProcessAr))
                        {
                            dbProcess = allProcesses.FirstOrDefault(p =>
                                !string.IsNullOrEmpty(p.NameAr) &&
                                (NormalizeArabic(p.NameAr).Contains(NormalizeArabic(procInfo.SubProcessAr)) ||
                                 NormalizeArabic(procInfo.SubProcessAr).Contains(NormalizeArabic(p.NameAr))));
                        }

                        // Fallback: try MainProcess name
                        if (dbProcess == null && !string.IsNullOrEmpty(procInfo.MainProcessAr))
                        {
                            dbProcess = allProcesses.FirstOrDefault(p =>
                                !string.IsNullOrEmpty(p.NameAr) &&
                                (NormalizeArabic(p.NameAr) == NormalizeArabic(procInfo.MainProcessAr) ||
                                 NormalizeArabic(p.NameAr).Contains(NormalizeArabic(procInfo.MainProcessAr)) ||
                                 NormalizeArabic(procInfo.MainProcessAr).Contains(NormalizeArabic(p.NameAr))));
                        }

                        // Fallback: try English names
                        if (dbProcess == null)
                        {
                            dbProcess = allProcesses.FirstOrDefault(p =>
                                !string.IsNullOrEmpty(p.NameEn) &&
                                (p.NameEn.Equals(procInfo.SubProcessEn, StringComparison.OrdinalIgnoreCase) ||
                                 p.NameEn.Equals(procInfo.MainProcessEn, StringComparison.OrdinalIgnoreCase)));
                        }

                        if (dbProcess != null)
                        {
                            matchedProcessId = dbProcess.Id;
                            matchedProcessName = dbProcess.NameAr;
                        }
                    }
                }

                // Strategy 2: Direct name match against Process names (fallback)
                if (matchedProcessId == null)
                {
                    var directMatch = allProcesses.FirstOrDefault(p =>
                        !string.IsNullOrEmpty(p.NameAr) &&
                        (NormalizeArabic(p.NameAr) == NormalizeArabic(procedureName) ||
                         NormalizeArabic(p.NameAr).Contains(NormalizeArabic(procedureName)) ||
                         NormalizeArabic(procedureName).Contains(NormalizeArabic(p.NameAr))));

                    if (directMatch != null)
                    {
                        matchedProcessId = directMatch.Id;
                        matchedProcessName = directMatch.NameAr;
                    }
                }

                if (matchedProcessId != null)
                {
                    matched.Add((file, matchedProcessId, xml, procedureName));
                    mappings.Add(new { file = fileName, processId = matchedProcessId, processNameAr = matchedProcessName, bpmnName = procedureName, status = "matched" });
                }
                else
                {
                    unmatched.Add(new { file = fileName, bpmnName = procedureName, reason = "No matching process found" });
                }
            }

            // If test only, return mapping without saving
            if (request.TestOnly)
            {
                // For AI test, optimize a few
                var aiResults = new List<object>();
                if (request.UseAiOptimization && matched.Any())
                {
                    var testItems = matched.Take(request.TestCount).ToList();
                    foreach (var item in testItems)
                    {
                        try
                        {
                            var optimized = await _aiService.RefineBPMNDiagramAsync(
                                item.bpmnXml,
                                "Optimize this BPMN diagram: 1) Add proper StartEvent and EndEvent if missing. 2) Convert tasks to proper BPMN elements (UserTask, ServiceTask, ManualTask as appropriate). 3) Add proper Gateways (Exclusive, Parallel) where needed. 4) Ensure proper sequence flow. 5) Keep all Arabic text. 6) Add proper BPMN DI layout coordinates for clean rendering. 7) Remove any non-BPMN artifacts.");

                            var cleanOptimized = _bpmnService.CleanBpmnXml(optimized);
                            var isValid = _bpmnService.LooksLikeBpmnXml(cleanOptimized);

                            aiResults.Add(new
                            {
                                file = Path.GetFileNameWithoutExtension(item.filePath),
                                processName = item.matchedName,
                                originalSize = item.bpmnXml.Length,
                                optimizedSize = cleanOptimized?.Length ?? 0,
                                isValid,
                                preview = cleanOptimized?.Substring(0, Math.Min(500, cleanOptimized?.Length ?? 0))
                            });
                        }
                        catch (Exception ex)
                        {
                            aiResults.Add(new
                            {
                                file = Path.GetFileNameWithoutExtension(item.filePath),
                                processName = item.matchedName,
                                error = ex.Message
                            });
                        }
                    }
                }

                return Json(new
                {
                    success = true,
                    testOnly = true,
                    totalFiles = bpmnFiles.Count,
                    matchedCount = matched.Count,
                    unmatchedCount = unmatched.Count,
                    mappings,
                    unmatched,
                    aiResults
                });
            }

            // === FULL IMPORT ===

            // Step 1: Clear existing BPMN + version history ONLY for the processes
            // we're about to re-import. Previously this nulled EVERY process's
            // diagram and deleted ALL ProcessBpmnVersions (no ProcessId filter),
            // so any process with a hand-authored diagram that isn't matched by
            // this folder lost both its diagram and its full version history.
            var matchedProcessIds = matched.Select(m => m.processId).Distinct().ToList();

            var processesToReset = await _context.Processes
                .Where(p => !p.IsDeleted && p.BpmnDiagram != null && matchedProcessIds.Contains(p.Id))
                .ToListAsync();
            foreach (var p in processesToReset)
            {
                p.BpmnDiagram = null;
                p.UpdatedAt = DateTime.UtcNow;
            }

            // Clear version records for the same matched set only.
            var existingVersions = await _context.ProcessBpmnVersions
                .Where(v => matchedProcessIds.Contains(v.ProcessId))
                .ToListAsync();
            _context.ProcessBpmnVersions.RemoveRange(existingVersions);
            await _context.SaveChangesAsync();

            // Step 2: Import matched BPMNs (group by process, pick largest BPMN per process)
            var groupedByProcess = matched
                .GroupBy(m => m.processId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.bpmnXml.Length).ToList());

            int imported = 0;
            var importResults = new List<object>();

            foreach (var group in groupedByProcess)
            {
                var item = group.Value.First(); // Pick the largest/most detailed BPMN
                var process = await _context.Processes.FindAsync(group.Key);
                if (process == null) continue;

                var bpmnXml = item.bpmnXml;

                // Optionally optimize with AI
                if (request.UseAiOptimization)
                {
                    try
                    {
                        var optimized = await _aiService.RefineBPMNDiagramAsync(
                            bpmnXml,
                            "Optimize this BPMN diagram: 1) Add proper StartEvent and EndEvent if missing. 2) Convert tasks to proper BPMN elements. 3) Add proper Gateways where needed. 4) Ensure proper sequence flow. 5) Keep all Arabic text. 6) Add proper BPMN DI layout coordinates. 7) Remove non-BPMN artifacts.");

                        var cleaned = _bpmnService.CleanBpmnXml(optimized);
                        if (_bpmnService.LooksLikeBpmnXml(cleaned))
                            bpmnXml = cleaned;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "AI optimization failed for {File}, using original", Path.GetFileName(item.filePath));
                    }
                }

                process.BpmnDiagram = bpmnXml;
                process.UpdatedAt = DateTime.UtcNow;

                // Create version record
                var versionRecord = new Models.APQC.ProcessBpmnVersion
                {
                    Id = Guid.NewGuid().ToString(),
                    ProcessId = process.Id,
                    VersionNumber = 1,
                    BpmnXml = bpmnXml,
                    ChangeDescription = "Imported from Visio conversion",
                    CreatedById = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    CreatedByName = User.Identity?.Name ?? "System",
                    CreatedAt = DateTime.UtcNow,
                    IsCurrent = true,
                    XmlSizeBytes = System.Text.Encoding.UTF8.GetByteCount(bpmnXml)
                };
                _context.ProcessBpmnVersions.Add(versionRecord);

                imported++;
                importResults.Add(new {
                    processId = process.Id,
                    processNameAr = process.NameAr,
                    file = Path.GetFileName(item.filePath),
                    totalBpmnsForProcess = group.Value.Count,
                    otherProcedures = group.Value.Skip(1).Select(x => x.matchedName).ToList()
                });
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Successfully imported {imported} BPMN diagrams out of {bpmnFiles.Count} files.",
                imported,
                totalFiles = bpmnFiles.Count,
                matchedCount = matched.Count,
                unmatchedCount = unmatched.Count,
                importResults,
                unmatched
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch BPMN import");
            return Json(new { success = false, error = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// Normalize Arabic text for fuzzy matching (remove diacritics, normalize alef/ya)
    /// </summary>
    private static string NormalizeArabic(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var normalized = text.Trim();
        // Remove Arabic diacritics (tashkeel)
        normalized = Regex.Replace(normalized, @"[\u0610-\u061A\u064B-\u065F\u0670\u06D6-\u06DC\u06DF-\u06E8\u06EA-\u06ED]", "");
        // Normalize Alef variants to plain Alef
        normalized = normalized.Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا").Replace("ٱ", "ا");
        // Normalize Ya variants
        normalized = normalized.Replace("ى", "ي");
        // Normalize Ta Marbuta
        normalized = normalized.Replace("ة", "ه");
        // Remove extra whitespace
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized;
    }
}

/// <summary>
/// Request model for BPMN diagram generation
/// </summary>
public class BPMNRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<string>? Steps { get; set; }
}

/// <summary>
/// Request model for refining BPMN diagrams
/// </summary>
public class RefineRequest
{
	/// <summary>
	/// Preferred: BPMN 2.0 XML
	/// </summary>
	public string? CurrentBpmnXml { get; set; }

	/// <summary>
	/// Legacy field name kept for backward compatibility; may contain BPMN XML.
	/// </summary>
	public string? CurrentMermaidCode { get; set; }
    public string Instructions { get; set; } = string.Empty;
}

/// <summary>
/// Request model for saving BPMN to process
/// </summary>
public class SaveBPMNRequest
{
    public string? ProcessId { get; set; }
    public string? ProcessName { get; set; }
    public string BpmnXml { get; set; } = string.Empty;
    public string? ChangeDescription { get; set; }
}

/// <summary>
/// Request model for comprehensive process analysis
/// </summary>
public class ProcessAnalysisRequest
{
    public string ProcessId { get; set; } = string.Empty;
    public string? Focus { get; set; }
}

/// <summary>
/// Request model for process group analysis
/// </summary>
public class ProcessGroupAnalysisRequest
{
    public string ProcessGroupId { get; set; } = string.Empty;
    public string? Focus { get; set; }
}

/// <summary>
/// Request model for prompt optimization
/// </summary>
public class OptimizePromptRequest
{
    public string Prompt { get; set; } = string.Empty;
    public bool IsArabic { get; set; }
}

/// <summary>
/// Request model for batch BPMN import
/// </summary>
public class BatchBpmnImportRequest
{
    public bool TestOnly { get; set; } = false;
    public int TestCount { get; set; } = 5;
    public bool UseAiOptimization { get; set; } = false;
}

/// <summary>
/// Maps procedure data from analysis_output.json
/// </summary>
public class ProcedureMapping
{
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string SubProcessAr { get; set; } = "";
    public string SubProcessEn { get; set; } = "";
    public string MainProcessAr { get; set; } = "";
    public string MainProcessEn { get; set; } = "";
}

public class AssistantRequest
{
    public string Question { get; set; } = string.Empty;
    public List<AssistantMessage>? ConversationHistory { get; set; }
}

public class AssistantMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
