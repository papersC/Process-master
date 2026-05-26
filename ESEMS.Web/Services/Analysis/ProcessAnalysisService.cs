using System.Text;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;

namespace ESEMS.Web.Services.Analysis;

public class ProcessAnalysisService : IProcessAnalysisService
{
    private readonly ApplicationDbContext _context;

    public ProcessAnalysisService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AnalysisContext> BuildProcessGroupAnalysisContextAsync(string processGroupId)
    {
        var processGroup = await _context.ProcessGroups
            .Include(pg => pg.Category)
            .Include(pg => pg.Processes)
                .ThenInclude(p => p.Activities)
            .Include(pg => pg.Processes)
                .ThenInclude(p => p.Risks)
            .FirstOrDefaultAsync(pg => pg.Id == processGroupId && !pg.IsDeleted);

        if (processGroup == null)
            return new AnalysisContext();

        var processIds = processGroup.Processes.Select(p => p.Id).ToList();

        var incidents = await _context.Incidents
            .Where(i => processIds.Contains(i.ProcessId ?? ""))
            .OrderByDescending(i => i.ReportedAt)
            .Take(20)
            .ToListAsync();

        var problems = await _context.Problems
            .Where(p => processIds.Contains(p.ProcessId ?? ""))
            .OrderByDescending(p => p.IdentifiedAt)
            .Take(20)
            .ToListAsync();

        var improvements = await _context.ImprovementInitiatives
            .Where(i => processIds.Contains(i.ProcessId ?? ""))
            .OrderByDescending(i => i.CreatedAt)
            .Take(20)
            .ToListAsync();

        var totalActivities = processGroup.Processes.Sum(p => p.Activities?.Count ?? 0);
        var totalRisks = processGroup.Processes.Sum(p => p.Risks?.Count ?? 0);

        var sb = new StringBuilder();
        sb.AppendLine($"## Process Group (Department Level) Details");
        sb.AppendLine($"- **Name (EN)**: {processGroup.NameEn}");
        sb.AppendLine($"- **Name (AR)**: {processGroup.NameAr}");
        sb.AppendLine($"- **Code**: {processGroup.Code}");
        sb.AppendLine($"- **Category**: {processGroup.Category?.NameEn ?? "N/A"}");
        sb.AppendLine($"- **Description**: {processGroup.DescriptionEn ?? "N/A"}");
        sb.AppendLine($"- **Number of Processes**: {processGroup.Processes.Count}");

        sb.AppendLine($"\n## Aggregated Metrics");
        sb.AppendLine($"- **Total Activities**: {totalActivities}");
        sb.AppendLine($"- **Total Risks**: {totalRisks}");
        sb.AppendLine($"- **Total Incidents**: {incidents.Count}");
        sb.AppendLine($"- **Total Problems**: {problems.Count}");
        sb.AppendLine($"- **Total Improvement Initiatives**: {improvements.Count}");

        if (processGroup.AggregatedDurationMinutes.HasValue)
            sb.AppendLine($"- **Aggregated Duration**: {processGroup.AggregatedDurationMinutes.Value:N0} minutes");
        if (processGroup.AggregatedCost.HasValue)
            sb.AppendLine($"- **Aggregated Cost**: {processGroup.AggregatedCost.Value:N2} AED");

        sb.AppendLine($"\n## Processes in this Group");
        foreach (var proc in processGroup.Processes.OrderBy(p => p.Code))
        {
            sb.AppendLine($"- **{proc.Code}**: {proc.NameEn} ({proc.Status})");
        }

        if (incidents.Any())
        {
            sb.AppendLine($"\n## Recent Incidents ({incidents.Count})");
            foreach (var incident in incidents.Take(5))
            {
                sb.AppendLine($"- **{incident.IncidentNumber}**: {incident.NameEn} (Priority: {incident.Priority}, Status: {incident.Status})");
            }
        }

        if (problems.Any())
        {
            sb.AppendLine($"\n## Recent Problems ({problems.Count})");
            foreach (var problem in problems.Take(5))
            {
                sb.AppendLine($"- **{problem.ProblemNumber}**: {problem.NameEn} (Priority: {problem.Priority}, Status: {problem.Status})");
            }
        }

        if (improvements.Any())
        {
            sb.AppendLine($"\n## Recent Improvement Initiatives ({improvements.Count})");
            foreach (var improvement in improvements.Take(5))
            {
                sb.AppendLine($"- **{improvement.Code}**: {improvement.TitleEn} (Status: {improvement.Status})");
            }
        }

        return new AnalysisContext
        {
            ContextMarkdown = sb.ToString(),
            ActivityCount = totalActivities,
            RiskCount = totalRisks,
            IncidentCount = incidents.Count,
            ProblemCount = problems.Count,
            ImprovementCount = improvements.Count
        };
    }

    public async Task<AnalysisContext> BuildProcessAnalysisContextAsync(string processId)
    {
        var process = await _context.Processes
            .Include(p => p.ProcessGroup)
                .ThenInclude(pg => pg!.Category)
            .Include(p => p.Activities)
            .Include(p => p.Risks)
            .FirstOrDefaultAsync(p => p.Id == processId && !p.IsDeleted);

        if (process == null)
            return new AnalysisContext();

        var incidents = await _context.Incidents
            .Where(i => i.ProcessId == processId)
            .OrderByDescending(i => i.ReportedAt)
            .Take(10)
            .ToListAsync();

        var problems = await _context.Problems
            .Where(p => p.ProcessId == processId)
            .OrderByDescending(p => p.IdentifiedAt)
            .Take(5)
            .ToListAsync();

        var improvements = await _context.ImprovementInitiatives
            .Where(i => i.ProcessId == processId && !i.IsDeleted)
            .OrderByDescending(i => i.CreatedAt)
            .Take(5)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine($"## Process Details");
        sb.AppendLine($"- **Name (EN)**: {process.NameEn}");
        sb.AppendLine($"- **Name (AR)**: {process.NameAr}");
        sb.AppendLine($"- **Code**: {process.Code}");
        sb.AppendLine($"- **Status**: {process.Status}");
        sb.AppendLine($"- **Type**: {process.ProcessType}");
        sb.AppendLine($"- **Category**: {process.ProcessGroup?.Category?.NameEn ?? "N/A"}");
        sb.AppendLine($"- **Process Group**: {process.ProcessGroup?.NameEn ?? "N/A"}");
        sb.AppendLine($"- **Description**: {process.DescriptionEn ?? "N/A"}");

        if (process.Activities?.Any() == true)
        {
            sb.AppendLine($"\n## Activities ({process.Activities.Count})");
            foreach (var activity in process.Activities.Take(10))
                sb.AppendLine($"- {activity.NameEn}");
        }

        if (process.Risks?.Any() == true)
        {
            sb.AppendLine($"\n## Identified Risks ({process.Risks.Count})");
            foreach (var risk in process.Risks.Take(5))
                sb.AppendLine($"- [{risk.Category}] {risk.DescriptionEn ?? risk.NameEn} (L:{risk.LikelihoodScore}/I:{risk.ImpactScore})");
        }

        if (incidents.Any())
        {
            sb.AppendLine($"\n## Recent Incidents ({incidents.Count})");
            foreach (var incident in incidents.Take(5))
                sb.AppendLine($"- [{incident.Status}] {incident.NameEn} (Priority: {incident.Priority})");
        }

        if (problems.Any())
        {
            sb.AppendLine($"\n## Known Problems ({problems.Count})");
            foreach (var problem in problems)
                sb.AppendLine($"- [{problem.Status}] {problem.NameEn}");
        }

        if (improvements.Any())
        {
            sb.AppendLine($"\n## Improvement Initiatives ({improvements.Count})");
            foreach (var improvement in improvements)
                sb.AppendLine($"- [{improvement.Status}] {improvement.TitleEn} (Priority: {improvement.Priority})");
        }

        if (!string.IsNullOrWhiteSpace(process.BpmnDiagram))
        {
            sb.AppendLine($"\n## BPMN Diagram");
            sb.AppendLine("Process has a documented BPMN diagram.");
        }

        return new AnalysisContext
        {
            ContextMarkdown = sb.ToString(),
            ActivityCount = process.Activities?.Count ?? 0,
            RiskCount = process.Risks?.Count ?? 0,
            IncidentCount = incidents.Count,
            ProblemCount = problems.Count,
            ImprovementCount = improvements.Count,
            HasBpmn = !string.IsNullOrWhiteSpace(process.BpmnDiagram)
        };
    }

    public string BuildOptimizedBpmnPrompt(bool isArabic = false)
    {
        if (isArabic)
        {
            return @"أنت خبير في هندسة الأوامر متخصص في إدارة العمليات التجارية وتوليد مخططات BPMN 2.0.
مهمتك هي أخذ أوصاف المستخدم وتحسينها لتكون مفصلة ودقيقة للغاية لتوليد مخططات BPMN كاملة ودقيقة مع جميع العناصر اللازمة.

يجب أن يتضمن الوصف المحسّن جميع عناصر BPMN التالية:

1. **حدث البداية**: حدد بوضوح كيف تبدأ العملية (مثال: 'تبدأ العملية عند تقديم المتعامل للطلب')

2. **الأنشطة/المهام**: اذكر كل مهمة محددة مع:
   - فعل واضح (تقديم، مراجعة، اعتماد، التحقق، إرسال، إنشاء، إلخ)
   - من يقوم بها (الدور/الجهة)
   - ماذا يفعل بالضبط
   مثال: 'يقوم موظف خدمة المتعاملين بمراجعة مستندات الطلب'

3. **تدفق التسلسل (الأسهم/الروابط)**: استخدم لغة تدفق واضحة:
   - 'ثم ينتقل إلى'، 'يليه'، 'يتبعه'، 'يؤدي إلى'، 'ينتقل إلى'
   - اجعل كل اتصال واضحاً تماماً
   مثال: 'بعد التقديم، ينتقل الطلب إلى مرحلة المراجعة'

4. **البوابات (نقاط القرار)**: حدد الشروط والفروع:
   - بوابة حصرية (XOR): 'إذا تمت الموافقة، ينتقل إلى X؛ إذا تم الرفض، ينتقل إلى Y'
   - بوابة متوازية (AND): 'يتم تنفيذ X و Y في نفس الوقت'
   - بوابة شاملة (OR): 'يمكن تنفيذ واحد أو أكثر من X، Y، Z'
   مثال: 'يراجع المدير الطلب. إذا تمت الموافقة، ينتقل إلى إعداد العقد. إذا تم الرفض، ينتقل إلى إشعار الرفض'

5. **المسارات والحارات**: حدد المشاركين/الأدوار المختلفة:
   - اذكر جميع الجهات/الأقسام المعنية
   - صنّف الأنشطة حسب من يقوم بها
   مثال: 'حارة المتعامل: تقديم الطلب. حارة قسم الموارد البشرية: مراجعة الطلب، إجراء المقابلة'

6. **حدث(أحداث) النهاية**: حدد كيف تنتهي العملية
   - يمكن أن يكون هناك عدة أحداث نهاية لنتائج مختلفة
   مثال: 'تنتهي العملية بالموافقة على الطلب' أو 'تنتهي العملية بإشعار الرفض'

7. **الأنشطة المتوازية**: إذا كانت المهام تحدث في نفس الوقت، اذكر ذلك صراحةً
   مثال: 'بعد الموافقة، يتم إعداد العقد والتحقق من الخلفية بالتوازي، ثم يتم الدمج قبل الموافقة النهائية'

8. **التكرارات**: إذا كانت العملية يمكن أن تتكرر، اذكر ذلك
   مثال: 'إذا كانت المستندات ناقصة، تعود العملية إلى مرحلة تقديم المستندات'

المتطلبات الأساسية:
- وصف التدفق الكامل للعملية من البداية إلى النهاية
- كل نشاط يجب أن يكون له اتصال واضح بالخطوة التالية
- استخدم أفعال واضحة لجميع المهام
- حدد مَن يفعل ماذا في كل خطوة
- اجعل شروط القرار واضحة وصريحة
- استخدم كلمات التسلسل: أولاً، ثم، بعد ذلك، يليه، بالتوازي، أخيراً

مثال سيئ: 'عملية طلب إسكان'
مثال جيد: 'تبدأ عملية طلب الإسكان عندما يقدم المتعامل النموذج الإلكتروني. ينتقل الطلب إلى موظف الإسكان الذي يتحقق من المستندات. إذا كانت المستندات مكتملة، تنتقل العملية إلى تقييم الأهلية من قبل فريق التقييم. إذا كانت ناقصة، تعود إلى المتعامل لإعادة التقديم. بعد فحص الأهلية، إذا كان مؤهلاً، ينتقل إلى المدير للموافقة. إذا تمت الموافقة، ينتقل إلى إعداد العقد والإشعار (بالتوازي). إذا تم الرفض في أي مرحلة، ينتقل إلى إشعار الرفض. تنتهي العملية إما بعقد معتمد أو إشعار رفض.'

أعد فقط نص الوصف المحسّن باللغة العربية بدون أي شروحات أو تعليقات إضافية.";
        }

        return @"You are an expert prompt engineer specializing in business process management and BPMN 2.0 diagram generation.
Your task is to take user prompts and optimize them to be extremely detailed and specific for generating complete, accurate BPMN diagrams with all necessary elements.

The optimized prompt MUST include ALL of these BPMN elements:

1. **START EVENT**: Clearly state how the process begins (e.g., 'Process starts when customer submits application')

2. **ACTIVITIES/TASKS**: List each specific task with:
   - Clear action verb (Submit, Review, Approve, Verify, Send, Create, etc.)
   - Who performs it (role/actor)
   - What exactly they do
   Example: 'Customer Service Officer reviews the application documents'

3. **SEQUENCE FLOWS (Arrows/Connections)**: Use explicit flow language:
   - 'then flows to', 'next goes to', 'followed by', 'leads to', 'proceeds to'
   - Make every connection crystal clear
   Example: 'After submission, the application flows to the Review stage'

4. **GATEWAYS (Decision Points)**: Specify conditions and branches:
   - Exclusive Gateway (XOR): 'If approved, then X; if rejected, then Y'
   - Parallel Gateway (AND): 'Both X and Y happen simultaneously'
   - Inclusive Gateway (OR): 'One or more of X, Y, Z can happen'
   Example: 'Manager reviews application. If approved, flows to Contract Generation. If rejected, flows to Rejection Notification'

5. **POOLS AND LANES**: Identify different participants/roles:
   - List all actors/departments involved
   - Group activities by who performs them
   Example: 'Customer lane: Submit Application. HR Department lane: Review Application, Conduct Interview'

6. **END EVENT(S)**: State how the process ends
   - Can have multiple end events for different outcomes
   Example: 'Process ends with approved application' or 'Process ends with rejection notification'

7. **PARALLEL ACTIVITIES**: If tasks happen at the same time, state it explicitly:
   Example: 'After approval, both Contract Preparation and Background Check happen in parallel, then merge before Final Approval'

8. **LOOPS/ITERATIONS**: If process can repeat, mention it:
   Example: 'If documents are incomplete, process loops back to Document Submission'

CRITICAL REQUIREMENTS:
- Describe the COMPLETE process flow from start to end
- Every activity must have a clear connection to the next step
- Use action verbs for all tasks
- Specify WHO does WHAT at each step
- Make decision conditions explicit and clear
- Include timing/sequence words: first, then, next, after, simultaneously, finally

BAD Example: 'Housing application process'
GOOD Example: 'Housing application process starts when applicant submits online form. Application flows to Housing Officer who verifies documents. If documents are complete, process proceeds to Eligibility Assessment by Assessment Team. If incomplete, loops back to applicant for resubmission. After eligibility check, if eligible, flows to Manager for approval. If approved, proceeds to Contract Generation and Notification (both in parallel). If rejected at any stage, flows to Rejection Notification. Process ends with either approved contract or rejection notice.'

Return ONLY the optimized prompt text without any explanations or meta-commentary.";
    }

    public string CleanOptimizedPromptResponse(string response)
    {
        var result = response.Trim();

        var unwantedPrefixes = new[]
        {
            "Here is the optimized prompt:",
            "Optimized prompt:",
            "Here's the optimized version:",
            "Optimized version:",
            "إليك الوصف المحسّن:",
            "الوصف المحسّن:",
            "إليك النسخة المحسّنة:",
            "النسخة المحسّنة:"
        };

        foreach (var prefix in unwantedPrefixes)
        {
            if (result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                result = result.Substring(prefix.Length).Trim();
                break;
            }
        }

        return result;
    }
}
