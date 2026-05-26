using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ESEMS.Web.Services.AI.Prompts;

namespace ESEMS.Web.Services.AI;

/// <summary>
/// Azure OpenAI implementation of AI service for MBRHE ESEMS.
/// </summary>
public class AzureOpenAIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _deploymentId;
    private readonly string _apiVersion;
    private readonly int _maxTokens;
    private readonly double _temperature;
    private readonly ILogger<AzureOpenAIService> _logger;

    public AzureOpenAIService(IConfiguration configuration, ILogger<AzureOpenAIService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("AzureOpenAI");

        _endpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new ArgumentNullException("AzureOpenAI:Endpoint is required");
        _apiKey = configuration["AzureOpenAI:ApiKey"] ?? throw new ArgumentNullException("AzureOpenAI:ApiKey is required");
        _deploymentId = configuration["AzureOpenAI:DeploymentId"] ?? "gpt-4o";
        _apiVersion = configuration["AzureOpenAI:ApiVersion"] ?? "2025-01-01-preview";
        _maxTokens = int.TryParse(configuration["AzureOpenAI:MaxTokens"], out var mt) ? mt : 4000;
        _temperature = double.TryParse(configuration["AzureOpenAI:Temperature"], out var t) ? t : 0.7;

        _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
    }

    private async Task<string> InvokeOpenAIAsync(string systemPrompt, string userPrompt)
    {
        try
        {
            var url = $"{_endpoint.TrimEnd('/')}/openai/deployments/{_deploymentId}/chat/completions?api-version={_apiVersion}";

            var requestBody = new
            {
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = _maxTokens,
                temperature = _temperature
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Azure OpenAI API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return $"Error: Azure OpenAI returned {response.StatusCode}. {responseBody}";
            }

            var result = JsonSerializer.Deserialize<OpenAIResponse>(responseBody);
            return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response generated.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking Azure OpenAI");
            return $"Error: {ex.Message}";
        }
    }

    private async Task<string> InvokeOpenAIWithHistoryAsync(string systemPrompt, List<ChatMessage> messages)
    {
        try
        {
            var url = $"{_endpoint.TrimEnd('/')}/openai/deployments/{_deploymentId}/chat/completions?api-version={_apiVersion}";

            var allMessages = new List<object> { new { role = "system", content = systemPrompt } };
            allMessages.AddRange(messages.Select(m => new { role = m.Role, content = m.Content }));

            var requestBody = new
            {
                messages = allMessages,
                max_tokens = _maxTokens,
                temperature = _temperature
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Azure OpenAI API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return $"Error: Azure OpenAI returned {response.StatusCode}. {responseBody}";
            }

            var result = JsonSerializer.Deserialize<OpenAIResponse>(responseBody);
            return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response generated.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking Azure OpenAI");
            return $"Error: {ex.Message}";
        }
    }

    // i18n: when the UI culture is Arabic, instruct the model to answer entirely in
    // Arabic so AI output matches the user's language. Appended to analysis/generation
    // system prompts. (Bilingual generation deliberately omits this.)
    private static string Lang() =>
        System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar")
            ? " You MUST respond ENTIRELY in Arabic (العربية) — all headings, analysis, recommendations, and bullet points in Arabic."
            : "";

    public async Task<string> GenerateProcessImprovementSuggestionsAsync(string processName, string processDescription, string? currentIssues = null)
    {
        var systemPrompt = "You are an expert in business process improvement and operational excellence for MBRHE (Mohammed Bin Rashid Housing Establishment)." + Lang();
        var userPrompt = $@"Process Name: {processName}
Process Description: {processDescription}
{(string.IsNullOrEmpty(currentIssues) ? "" : $"Current Issues: {currentIssues}")}

Please provide:
1. 3-5 specific improvement suggestions
2. Expected benefits for each suggestion
3. Implementation difficulty (Low/Medium/High)
4. Estimated impact (Low/Medium/High)

Format your response in a clear, structured way.";

        return await InvokeOpenAIAsync(systemPrompt, userPrompt);
    }

    public async Task<string> AnalyzeRiskAndSuggestMitigationAsync(string riskDescription, string riskCategory, int likelihood, int impact)
    {
        var systemPrompt = "You are a risk management expert for MBRHE (Mohammed Bin Rashid Housing Establishment)." + Lang();
        var userPrompt = $@"Risk Description: {riskDescription}
Risk Category: {riskCategory}
Likelihood (1-10): {likelihood}
Impact (1-10): {impact}

Please provide:
1. Risk analysis and severity assessment
2. 3-5 specific mitigation strategies
3. Preventive measures
4. Contingency plans

Format your response in a clear, structured way.";

        return await InvokeOpenAIAsync(systemPrompt, userPrompt);
    }

    public async Task<string> GenerateRACISuggestionsAsync(string activityName, string activityDescription, string organizationContext)
    {
        var systemPrompt = "You are an organizational design expert for MBRHE (Mohammed Bin Rashid Housing Establishment)." + Lang();
        var userPrompt = $@"Activity Name: {activityName}
Activity Description: {activityDescription}
Organization Context: {organizationContext}

Please suggest a RACI matrix (Responsible, Accountable, Consulted, Informed) for this activity.
Provide role suggestions and explain the reasoning for each assignment.

Format your response in a clear, structured way.";

        return await InvokeOpenAIAsync(systemPrompt, userPrompt);
    }

    public async Task<string> SummarizeAuditLogsAsync(List<string> auditLogEntries, string timeframe)
    {
        var logsText = string.Join("\n", auditLogEntries.Take(100));
        var systemPrompt = "You are a data analyst specializing in audit log analysis for MBRHE." + Lang();
        var userPrompt = $@"Timeframe: {timeframe}
Audit Log Entries:
{logsText}

Please provide:
1. Summary of key activities
2. Notable patterns or anomalies
3. User activity highlights
4. Recommendations for security or compliance

Format your response in a clear, structured way.";

        return await InvokeOpenAIAsync(systemPrompt, userPrompt);
    }

    public async Task<string> GenerateStrategicObjectiveRecommendationsAsync(string organizationGoals, string currentObjectives)
    {
        var systemPrompt = "You are a strategic planning consultant for MBRHE (Mohammed Bin Rashid Housing Establishment)." + Lang();
        var userPrompt = $@"Organization Goals: {organizationGoals}
Current Objectives: {currentObjectives}

Please provide:
1. Analysis of current objectives alignment with goals
2. 3-5 new strategic objective recommendations
3. Key performance indicators (KPIs) for each objective
4. Implementation priorities

Format your response in a clear, structured way.";

        return await InvokeOpenAIAsync(systemPrompt, userPrompt);
    }

    public async Task<string> AnalyzeServicePerformanceAsync(string serviceName, decimal? satisfactionScore, int? transactionCount, string? issues)
    {
        var systemPrompt = "You are a service management expert for MBRHE." + Lang();
        var userPrompt = $@"Service Name: {serviceName}
Customer Satisfaction Score: {satisfactionScore?.ToString() ?? "N/A"}
Annual Transaction Count: {transactionCount?.ToString() ?? "N/A"}
{(string.IsNullOrEmpty(issues) ? "" : $"Known Issues: {issues}")}

Please provide:
1. Performance analysis
2. Areas for improvement
3. Specific recommendations
4. Best practices to implement

Format your response in a clear, structured way.";

        return await InvokeOpenAIAsync(systemPrompt, userPrompt);
    }

    public async Task<(string English, string Arabic)> GenerateBilingualContentAsync(string prompt, string context)
    {
        var systemPrompt = "You are a bilingual content generator fluent in English and Arabic for MBRHE.";
        var userPrompt = $@"Context: {context}
Request: {prompt}

Please provide the content in both languages:
1. First in English
2. Then in Arabic

Format:
ENGLISH:
[English content here]

ARABIC:
[Arabic content here]";

        var response = await InvokeOpenAIAsync(systemPrompt, userPrompt);
        var parts = response.Split(new[] { "ENGLISH:", "ARABIC:" }, StringSplitOptions.RemoveEmptyEntries);
        string english = parts.Length > 0 ? parts[0].Trim() : response;
        string arabic = parts.Length > 1 ? parts[1].Trim() : "";
        return (english, arabic);
    }

    public async Task<string> ChatAsync(string userMessage, List<(string role, string content)>? conversationHistory = null)
    {
        var systemPrompt = @"You are an expert AI assistant for MBRHE's Environmental and Safety Excellence Management System (ESEMS).

# MBRHE CONTEXT
Mohammed Bin Rashid Housing Establishment (MBRHE) is a UAE government entity that provides housing solutions to Emirati citizens. ESEMS manages their business processes, quality, risk, and service management.

# ESEMS MODULES YOU CAN HELP WITH
1. **Process Management (APQC Framework)**:
   - 5-level hierarchy: Category → Process Group → Process → Activity → Task
   - Process types: Core, Support, Management
   - RACI matrix for responsibility assignment
   - Process performance measurement and KPIs

2. **Risk Management (ISO 31000:2018)**:
   - Enterprise risks with inherent and residual scoring
   - Risk likelihood (1-10) × Impact (1-10) = Risk Score
   - Risk categories: Operational, Strategic, Financial, Compliance, Reputational
   - Risk action plans with mitigation strategies
   - Risk appetite and tolerance levels

3. **Improvement Management**:
   - Improvement initiatives with ROI tracking
   - Status workflow: Proposed → Approved → In Progress → Completed
   - Kanban board for visual management
   - Cost-benefit analysis

4. **Service Management (ISO 20000-1:2018)**:
   - Service catalog with SLA definitions
   - Customer satisfaction tracking
   - Service performance metrics

5. **Incident & Problem Management**:
   - Incident tracking with priority levels (Critical, High, Medium, Low)
   - SLA response and resolution times
   - Problem management with root cause analysis
   - Known error database

6. **Asset Management (ISO 55001:2014)**:
   - Asset lifecycle management
   - Maintenance scheduling (Preventive, Corrective, Predictive)
   - Asset condition monitoring

7. **Customer Feedback**:
   - Complaints, suggestions, and compliments
   - Root cause analysis and corrective actions
   - Customer satisfaction surveys

8. **SLA Management**:
   - SLA definitions with target metrics
   - Breach tracking and severity assessment
   - Service level reporting

# ISO COMPLIANCE GUIDANCE
- **ISO 9001:2015 (Quality)**: Document control, process approach, continual improvement
- **ISO 20000-1:2018 (IT Service)**: Service lifecycle, incident/problem management
- **ISO 31000:2018 (Risk)**: Risk framework, assessment, treatment, monitoring
- **ISO 55001:2014 (Asset)**: Asset lifecycle, performance, risk-based decisions

# BPMN 2.0 PROCESS MODELING
When discussing process modeling:
- Explain BPMN elements: Events, Activities, Gateways, Sequence Flows
- Discuss pools/lanes for organizational responsibilities
- Recommend collaboration diagrams for cross-departmental processes

# YOUR APPROACH
- Provide actionable, specific guidance with examples
- Reference relevant ISO standards when applicable
- Suggest ESEMS features that can address user needs
- Use structured responses with bullet points and headings
- Consider MBRHE's government context (Arabic/English bilingual, UAE regulations)";

        var messages = new List<ChatMessage>();
        if (conversationHistory != null)
        {
            foreach (var (role, content) in conversationHistory)
            {
                if (!string.IsNullOrWhiteSpace(content))
                    messages.Add(new ChatMessage { Role = role == "assistant" ? "assistant" : "user", Content = content });
            }
        }
        messages.Add(new ChatMessage { Role = "user", Content = userMessage });

        return await InvokeOpenAIWithHistoryAsync(systemPrompt, messages);
    }

    public async Task<string> GenerateBPMNDiagramAsync(string processName, string processDescription, List<string>? steps = null, bool? hintArabic = null)
    {
        // hintArabic lets callers (e.g. the anonymous batch importer) force
        // Arabic output when CurrentUICulture is en-US but the source
        // content is Arabic. Falls back to culture + content sniffing.
        var isArabic = hintArabic
            ?? System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar")
            || ContainsArabicScript(processName)
            || ContainsArabicScript(processDescription);
        var languageRule = isArabic
            ? @"
## LANGUAGE RULE (CRITICAL)
You MUST generate ALL diagram text in Arabic. This includes:
- Pool names (e.g. name=""المتعامل"", name=""مؤسسة محمد بن راشد للإسكان"")
- Lane names (e.g. name=""إسعاد المتعاملين"", name=""المشاريع الهندسية"", name=""الإدارة"")
- Task/activity labels (e.g. name=""استلام الطلب"", name=""مراجعة المستندات"", name=""اعتماد الطلب"")
- Event names (e.g. name=""بداية"", name=""نهاية"", name=""طلب مرفوض"")
- Gateway flow labels (e.g. name=""موافق"", name=""مرفوض"")
- Message flow labels (e.g. name=""تقديم الطلب"", name=""استلام النتيجة"")
Use raw UTF-8 Arabic characters. NEVER use XML numeric entities."
            : "\n## LANGUAGE RULE\nYou MUST generate all diagram text, element names, task labels, and pool names exclusively in the English language.";

        var stepsText = steps != null && steps.Any()
            ? $"\nProcess Steps:\n{string.Join("\n", steps.Select((s, i) => $"{i + 1}. {s}"))}"
            : "";

        // Plan: inject the shared MBRHE bilingual glossary so task labels,
        // events, and message flows use canonical terminology instead of
        // translated-on-the-fly phrases. Consistent vocabulary across the
        // Analyzer and the BPMN generator means every AI surface speaks
        // the same domain language.
        var glossary = MbrheGlossary.ForCulture(isArabic);

        var systemPrompt = $@"You are an expert BPMN 2.0 modeler for MBRHE (Mohammed Bin Rashid Housing Establishment) — a Dubai government entity that provides housing services (loans, grants, land allocation) to UAE nationals.

{glossary}

## MBRHE ORGANIZATION STRUCTURE (use these as lane names)
Executive: CEO Office (CEOO), Internal Audit (IAO), Legal Affairs (LEG), Strategy & Development (STR)
Corporate Support Sector: Support Services (SSV), Digital Transformation (DIG), Communication & Marketing (COM)
  - SSV sections: Planning & Budget, Revenue & Collection, Contracts & Procurement, Human Resources, Admin Affairs
  - DIG sections: Smart Systems Development, Technical Support
Housing Sector: Engineering Projects (ENG), Customer Happiness (CHP), Investment (INV), External Branches (EXT)
  - ENG sections: Planning & Design, Engineering Supervision, Maintenance, Assets
  - CHP sections: Service Excellence, Customer Care, Request Processing
External: Customer/Applicant (external participant)
Systems: HMS (Housing Management), CPL (Customer Portal), ERP (Finance/HR), CRM (Customer Relations), UAEPASS (Digital ID)

## MBRHE SERVICES (12 housing services)
Loan services: Purchase Loan, Construction Loan, Replacement Loan, Maintenance Loan
Grant services: Land Grant, House Grant, Apartment Grant, Construction Grant, Maintenance Grant

## LAYOUT RULES — elements must NEVER overlap. Leave generous spacing.
## TEXT ENCODING — use raw UTF-8 Arabic characters directly (e.g. name=""تقديم الطلب""). NEVER use XML numeric entities like &#x62C; for Arabic text.{languageRule}
Output ONLY raw BPMN 2.0 XML — never markdown fences, code blocks, or explanations.";

        var archSection = isArabic
            ? $@"## البنية — مسبحان: المتعامل (خارجي) + مؤسسة محمد بن راشد للإسكان (مع حارات)
- المتعامل خارجي — استخدم مسبح منفصل (مطوي، بدون عناصر بداخله). اسم المسبح: ""المتعامل""
- مسبح المؤسسة هو المسبح الرئيسي مع 2-3 حارات أفقية تمثل الإدارات الداخلية.
- استخدم <bpmn:messageFlow> للربط بين مسبح المتعامل ومسبح المؤسسة — وليس sequenceFlow.
- اختر الحارات من إدارات المؤسسة فقط: إسعاد المتعاملين، المشاريع الهندسية، خدمات الدعم، التحول الرقمي، الشؤون القانونية، الإدارة.
- كل حارة يجب أن تحتوي على مهمة واحدة على الأقل.
- اجعل أسماء المهام قصيرة (4 كلمات كحد أقصى). مثال: ""استلام الطلب""، ""التحقق من المستندات""، ""اعتماد الطلب"".
- يجب ألا تمر الأسهم عبر أي عنصر."
            : @"## ARCHITECTURE — TWO POOLS: Customer (external) + MBRHE (with lanes)
- The Customer/Applicant is EXTERNAL to MBRHE — use a SEPARATE pool (collapsed, no process inside).
- MBRHE is the main pool with 2-3 horizontal <bpmn:lane> elements showing internal departments.
- Use <bpmn:messageFlow> (in <bpmn:collaboration>) to connect Customer pool to MBRHE pool — NOT sequenceFlow.
- Pick lanes from MBRHE departments ONLY: Customer Happiness, Engineering Projects, Support Services, Digital Transformation, Legal Affairs, Management.
- Each lane MUST contain at least 1 task. Every task/event/gateway ID must appear in exactly one lane's <bpmn:flowNodeRef> list.
- Keep task labels SHORT (max 4 words). Example: ""Receive Application"", ""Verify Documents"", ""Approve Request"".
- Arrows must NEVER cross over/through any element (task, event, gateway). Route around them.";

        var flowSection = isArabic
            ? @"## تدفق العملية (إلزامي)
1. مسبح المتعامل: مطوي (بدون عناصر بداخله). مستطيل مسمى فقط.
2. مسبح المؤسسة: يحتوي على جميع عناصر العملية داخل الحارات.
3. حدث بداية واحد <bpmn:startEvent> في أول حارة (مثال: إسعاد المتعاملين)
4. ثم 5-7 أنشطة متسلسلة (<bpmn:userTask> للمهام البشرية، <bpmn:serviceTask> للنظام)
5. استخدم <bpmn:exclusiveGateway> للقرارات — بوابة واحدة كحد أقصى
6. البوابة يجب أن يكون لها تدفقان صادران مع name=""موافق""/""مرفوض""
7. فرع الرفض: مهمة واحدة + حدث نهاية واحد <bpmn:endEvent>
8. فرع الموافقة يستمر إلى حدث نهاية واحد <bpmn:endEvent>
9. اربط جميع العناصر بـ <bpmn:sequenceFlow> — لا عناصر معزولة
10. أضف 1-2 <bpmn:messageFlow> في التعاون لربط مسبح المتعامل بأحداث بداية/نهاية المؤسسة"
            : @"## PROCESS FLOW (MANDATORY)
1. Customer pool: collapsed (no elements inside). Just a labeled rectangle.
2. MBRHE pool: contains ALL process elements inside lanes.
3. Exactly 1 <bpmn:startEvent> in the first MBRHE lane (e.g. Customer Happiness)
4. Then 5-7 sequential activities (<bpmn:userTask> for human, <bpmn:serviceTask> for system)
5. Use <bpmn:exclusiveGateway> for decisions — MAX 1 gateway per diagram
6. Gateway MUST have exactly 2 outgoing <bpmn:sequenceFlow> with name=""Approved""/""Rejected""
7. The rejected branch: 1 task + 1 <bpmn:endEvent>
8. The approved branch continues to 1 <bpmn:endEvent>
9. Connect ALL elements with <bpmn:sequenceFlow> — zero orphan elements
10. Add 1-2 <bpmn:messageFlow> in the collaboration connecting Customer pool to MBRHE start/end events";

        var userPrompt = $@"Generate a BPMN 2.0 XML diagram for bpmn-js.

Process Name: {processName}
Process Description: {processDescription}{stepsText}

{archSection}

{flowSection}

## LAYOUT (CRITICAL — follow exactly)

### Pool & Lane dimensions:
- Customer pool (collapsed): x=100, y=30, width=2200, height=60, isHorizontal=""true""
- MBRHE pool: x=100, y=120, width=2200, isHorizontal=""true""
- MBRHE pool height: (number_of_lanes × 150) — e.g. 3 lanes = 450
- Lane 1: x=130, y=120, width=2170, height=150
- Lane 2: x=130, y=270, width=2170, height=150
- Lane 3: x=130, y=420, width=2170, height=150
- Lane 4: x=130, y=570, width=2170, height=150 (if needed)

### Element sizes (EXACT):
- Tasks: width=""150"" height=""80""
- Events: width=""36"" height=""36""
- Gateways: width=""50"" height=""50""

### Element positioning:
{(isArabic
? @"- IMPORTANT: This is an RTL (Right-to-Left) Arabic diagram. The process flows from RIGHT to LEFT.
- X positions right-to-left: 1840, 1610, 1380, 1150, 920, 690, 460, 230 (spacing = 230px)
- The START event goes at x=1840 (rightmost). The END event goes at x=230 (leftmost).
- Each subsequent step moves LEFT (decreasing x)."
: @"- X positions left-to-right: 230, 460, 690, 920, 1150, 1380, 1610, 1840 (spacing = 230px)")}
- Y position: CENTER of the MBRHE lane the element belongs to.
  Lane 1 center: y=195 → task y=155, event y=177, gateway y=170
  Lane 2 center: y=345 → task y=305, event y=327, gateway y=320
  Lane 3 center: y=495 → task y=455, event y=477, gateway y=470
  Lane 4 center: y=645 → task y=605, event y=627, gateway y=620
- Elements in the SAME lane stay on the same y.
- Arrows must NEVER pass through any element — route the vertical segment through empty space between x positions.

### BPMNEdge Waypoints — ORTHOGONAL ONLY (no diagonal lines!):
All arrows must be strictly horizontal or vertical. NEVER draw diagonal lines.
{(isArabic
? @"
- **Same lane (horizontal, RTL):** 2 waypoints — source LEFT edge → target RIGHT edge, same y.
  Example (Task at x=1840 w=150 to Task at x=1610 w=150, both in Lane 1 y=105):
  waypoint x=""1840"" y=""105"", waypoint x=""1760"" y=""105""

- **Cross-lane (different y, RTL):** Use 4 waypoints to make an L-shape:
  Step 1: source LEFT edge (x, src_y)
  Step 2: go LEFT to midpoint x (halfway between source left and target right+w), same src_y
  Step 3: go straight DOWN/UP to target lane center y, same midpoint x
  Step 4: go LEFT to target RIGHT edge (tgt_x+w, tgt_y)

- **Gateway approved (same lane, left):** 2 waypoints — gateway LEFT edge → next element RIGHT edge.
- **Gateway rejected (cross-lane, RTL):** 4 waypoints orthogonal L-shape:
  waypoint x=""gw_x"" y=""gw_y+25"" (left edge)
  waypoint x=""midpoint_x"" y=""gw_y+25"" (go left)
  waypoint x=""midpoint_x"" y=""target_y"" (go down)
  waypoint x=""target_x+w"" y=""target_y"" (go left to target)"
: @"
- **Same lane (horizontal):** 2 waypoints — source RIGHT edge → target LEFT edge, same y.
  Example (Task at x=230 w=150 to Task at x=460 w=150, both in Lane 1 y=105):
  waypoint x=""380"" y=""105"", waypoint x=""460"" y=""105""

- **Cross-lane (different y):** Use 4 waypoints to make an L-shape or Z-shape:
  Step 1: source RIGHT edge (x+w, src_y)
  Step 2: go RIGHT to midpoint x (halfway between source right and target left), same src_y
  Step 3: go straight DOWN/UP to target lane center y, same midpoint x
  Step 4: go RIGHT to target LEFT edge (tgt_x, tgt_y)
  Example (Task in Lane 1 y=105, x=230 w=150 → Task in Lane 2 y=255, x=460 w=150):
  waypoint x=""380"" y=""105"", waypoint x=""420"" y=""105"", waypoint x=""420"" y=""255"", waypoint x=""460"" y=""255""

- **Gateway approved (same lane, right):** 2 waypoints — gateway RIGHT edge → next element LEFT edge.
- **Gateway rejected (cross-lane):** 4 waypoints orthogonal L-shape:
  waypoint x=""gw_x+50"" y=""gw_y+25"" (right edge)
  waypoint x=""midpoint_x"" y=""gw_y+25"" (go right)
  waypoint x=""midpoint_x"" y=""target_y"" (go down)
  waypoint x=""target_x"" y=""target_y"" (go right to target)")}

## XML STRUCTURE (follow this skeleton)
```
<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
  xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI""
  xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC""
  xmlns:di=""http://www.omg.org/spec/DD/20100524/DI""
  id=""Definitions_1"" targetNamespace=""http://bpmn.io/schema/bpmn"">
  <bpmn:collaboration id=""Collaboration_1"">
    <bpmn:participant id=""Customer_Pool"" name=""{(isArabic ? "المتعامل" : "Customer / Applicant")}"" />
    <bpmn:participant id=""MBRHE_Pool"" name=""PROCESS_NAME"" processRef=""Process_1"" />
    <bpmn:messageFlow id=""MsgFlow_1"" name=""{(isArabic ? "تقديم الطلب" : "Submit Request")}"" sourceRef=""Customer_Pool"" targetRef=""Start_1"" />
    <bpmn:messageFlow id=""MsgFlow_2"" name=""{(isArabic ? "استلام النتيجة" : "Receive Result")}"" sourceRef=""End_1"" targetRef=""Customer_Pool"" />
  </bpmn:collaboration>
  <bpmn:process id=""Process_1"" isExecutable=""false"">
    <bpmn:laneSet id=""LaneSet_1"">
      <bpmn:lane id=""Lane_1"" name=""{(isArabic ? "إسعاد المتعاملين" : "Customer Happiness")}"">
        <bpmn:flowNodeRef>Start_1</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_1</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_1</bpmn:flowNodeRef>
      </bpmn:lane>
      <bpmn:lane id=""Lane_2"" name=""{(isArabic ? "المشاريع الهندسية" : "Engineering Projects")}"">
        <bpmn:flowNodeRef>Task_2</bpmn:flowNodeRef>
      </bpmn:lane>
      <bpmn:lane id=""Lane_3"" name=""{(isArabic ? "الإدارة" : "Management")}"">
        <bpmn:flowNodeRef>Task_3</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>End_1</bpmn:flowNodeRef>
      </bpmn:lane>
    </bpmn:laneSet>
    <bpmn:startEvent id=""Start_1"" name=""{(isArabic ? "استلام الطلب" : "Request Received")}"">
      <bpmn:outgoing>Flow_1</bpmn:outgoing>
    </bpmn:startEvent>
    <bpmn:userTask id=""Task_1"" name=""{(isArabic ? "مراجعة الطلب" : "Review Application")}"">
      <bpmn:incoming>Flow_1</bpmn:incoming>
      <bpmn:outgoing>Flow_2</bpmn:outgoing>
    </bpmn:userTask>
    <!-- more tasks, gateways, endEvents -->
    <!-- CRITICAL: ALL sequenceFlow elements MUST be defined -->
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""Start_1"" targetRef=""Task_1"" />
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_1"" targetRef=""Gateway_1"" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Collaboration_1"">
      <!-- Customer pool (collapsed - no elements inside) -->
      <bpmndi:BPMNShape id=""Customer_Pool_di"" bpmnElement=""Customer_Pool"" isHorizontal=""true"">
        <dc:Bounds x=""100"" y=""30"" width=""2200"" height=""60"" />
      </bpmndi:BPMNShape>
      <!-- MBRHE pool -->
      <bpmndi:BPMNShape id=""MBRHE_Pool_di"" bpmnElement=""MBRHE_Pool"" isHorizontal=""true"">
        <dc:Bounds x=""100"" y=""120"" width=""2200"" height=""450"" />
      </bpmndi:BPMNShape>
      <!-- Lanes inside MBRHE pool -->
      <bpmndi:BPMNShape id=""Lane_1_di"" bpmnElement=""Lane_1"" isHorizontal=""true"">
        <dc:Bounds x=""130"" y=""120"" width=""2170"" height=""150"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Lane_2_di"" bpmnElement=""Lane_2"" isHorizontal=""true"">
        <dc:Bounds x=""130"" y=""270"" width=""2170"" height=""150"" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Lane_3_di"" bpmnElement=""Lane_3"" isHorizontal=""true"">
        <dc:Bounds x=""130"" y=""420"" width=""2170"" height=""150"" />
      </bpmndi:BPMNShape>
      <!-- BPMNShape for EVERY element, BPMNEdge for EVERY sequenceFlow and messageFlow -->
      <!-- messageFlow edges: vertical dashed from Customer pool bottom to MBRHE start event -->
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>
```

## VALIDATION CHECKLIST
1. CRITICAL: Every flow in <bpmn:incoming>/<bpmn:outgoing> has a MATCHING <bpmn:sequenceFlow>
2. Customer pool is SEPARATE (collapsed, no process) — connected via <bpmn:messageFlow> only
3. Every element ID appears in exactly ONE MBRHE lane's <bpmn:flowNodeRef> list
4. Every element has a BPMNShape with dc:Bounds using EXACT sizes above
5. Every sequenceFlow has a BPMNEdge with di:waypoint elements
6. Gateway has exactly 2 outgoing sequenceFlows with name attributes
7. All sourceRef/targetRef reference existing element IDs
8. No orphan elements — everything connected
9. Elements in the same lane share the same y center
10. X positions are at least 230px apart — NO overlaps
11. Arrows NEVER cross through any element — route through empty space
12. Task labels are SHORT (max 4 words)

Return ONLY the complete BPMN 2.0 XML. No markdown fences, no explanations.";

        return await InvokeOpenAIAsync(systemPrompt, userPrompt);
    }

    public async Task<string> RefineBPMNDiagramAsync(string currentBpmnXml, string updateInstructions)
    {
        var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
        var languageRule = isArabic
            ? @"
## LANGUAGE RULE (CRITICAL)
You MUST generate ALL diagram text in Arabic. This includes:
- Pool names (e.g. name=""المتعامل"", name=""مؤسسة محمد بن راشد للإسكان"")
- Lane names (e.g. name=""إسعاد المتعاملين"", name=""المشاريع الهندسية"", name=""الإدارة"")
- Task/activity labels (e.g. name=""استلام الطلب"", name=""مراجعة المستندات"", name=""اعتماد الطلب"")
- Event names (e.g. name=""بداية"", name=""نهاية"", name=""طلب مرفوض"")
- Gateway flow labels (e.g. name=""موافق"", name=""مرفوض"")
- Message flow labels (e.g. name=""تقديم الطلب"", name=""استلام النتيجة"")
Use raw UTF-8 Arabic characters. NEVER use XML numeric entities."
            : "\n## LANGUAGE RULE\nYou MUST generate all diagram text, element names, task labels, and pool names exclusively in the English language.";

        // Reuse the shared MBRHE bilingual glossary for terminology consistency
        var glossary = MbrheGlossary.ForCulture(isArabic);

        var systemPrompt = $@"You are a BPMN 2.0 expert for MBRHE (Mohammed Bin Rashid Housing Establishment).
You update existing BPMN 2.0 XML diagrams while maintaining clean layout, lanes, and bpmn-js compatibility.

{glossary}

Elements must NEVER overlap. Keep generous spacing (230px between elements horizontally, 150px per lane vertically).
Use raw UTF-8 Arabic characters directly — NEVER use XML numeric entities like &#x62C; for Arabic text.{languageRule}
Output ONLY raw XML — never markdown fences or explanations.";

        var userPrompt = $@"Update the BPMN XML below according to the user's instructions.

## CURRENT BPMN XML:
{currentBpmnXml}

## USER INSTRUCTIONS:
{updateInstructions}

## RULES:
1. Return ONLY the complete updated BPMN 2.0 XML — no markdown fences, no explanations.
2. Preserve ALL existing element IDs that are unchanged.
3. For NEW elements: add BPMNShape with dc:Bounds, <bpmn:incoming>/<bpmn:outgoing>, and add ID to the correct lane's <bpmn:flowNodeRef>.
4. For REMOVED elements: remove BPMNShape/BPMNEdge, update refs, and remove from lane flowNodeRef.
5. Recalculate BPMNEdge waypoints for moved/new elements. Cross-lane flows need intermediate waypoints.
6. Layout: Tasks=150x80, Events=36x36, Gateways=50x50. Lane height=150px. X spacing=230px.
7. If adding/removing lanes: adjust pool height (lanes × 150), shift all subsequent lane y positions.
8. Every gateway must have exactly 2 named outgoing flows.
9. All sourceRef/targetRef must reference existing element IDs.
10. Every element ID must appear in exactly one lane's flowNodeRef.
11. Task labels SHORT (max 4 words). NO overlaps.

Return ONLY BPMN XML.";

        return await InvokeOpenAIAsync(systemPrompt, userPrompt);
    }

    public async Task<string> ConvertVisioToBPMNAsync(string visioXmlContent, string? additionalContext = null, IReadOnlyList<string>? previousErrors = null)
    {
        // Arabic detection:
        //   1. CurrentUICulture=ar* (normal MVC request path)
        //   2. Fallback: scan the additionalContext (sheet name usually) AND the
        //      first chunk of the Visio XML for Arabic codepoints. Matters for
        //      the [AllowAnonymous] batch importer which has no culture cookie
        //      and was silently emitting English labels on Arabic source content.
        var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar")
            || ContainsArabicScript(additionalContext)
            || ContainsArabicScript(visioXmlContent.Length > 3000 ? visioXmlContent[..3000] : visioXmlContent);
        var languageRule = isArabic
            ? @"
## LANGUAGE RULE (CRITICAL)
You MUST generate ALL diagram text in Arabic. This includes:
- Pool names (e.g. name=""المتعامل"", name=""مؤسسة محمد بن راشد للإسكان"")
- Lane names (e.g. name=""إسعاد المتعاملين"", name=""المشاريع الهندسية"", name=""الإدارة"")
- Task/activity labels (e.g. name=""استلام الطلب"", name=""مراجعة المستندات"", name=""اعتماد الطلب"")
- Event names (e.g. name=""بداية"", name=""نهاية"", name=""طلب مرفوض"")
- Gateway flow labels (e.g. name=""موافق"", name=""مرفوض"")
- Message flow labels (e.g. name=""تقديم الطلب"", name=""استلام النتيجة"")
Use raw UTF-8 Arabic characters. NEVER use XML numeric entities."
            : "\n## LANGUAGE RULE\nYou MUST generate all diagram text, element names, task labels, and pool names exclusively in the English language.";

        var truncatedContent = visioXmlContent.Length > 15000
            ? visioXmlContent.Substring(0, 15000) + "\n... (truncated)"
            : visioXmlContent;

        var contextText = !string.IsNullOrWhiteSpace(additionalContext)
            ? $"\nAdditional Context: {additionalContext}"
            : "";

        var retryBlock = previousErrors is { Count: > 0 }
            ? $@"

## RETRY — YOUR PREVIOUS ATTEMPT FAILED VALIDATION
These errors MUST be fixed this time:
{string.Join("\n", previousErrors.Take(8).Select(e => "  - " + e))}

Pay particular attention to:
  - Every sequenceFlow sourceRef AND targetRef MUST match an element id that exists in the process.
  - Every <bpmndi:BPMNShape bpmnElement='x'/> and <bpmndi:BPMNEdge bpmnElement='x'/> MUST reference an id that exists in the bpmn:process tree.
  - Do not leave dangling refs or empty ids.
"
            : "";

        // Reuse the shared MBRHE bilingual glossary for terminology consistency
        var convertGlossary = MbrheGlossary.ForCulture(isArabic);

        var systemPrompt = $@"You are an expert Visio to BPMN 2.0 converter for MBRHE business processes.
You specialize in converting Visio.Drawing.15 format (.vsdx files from Visio 2013-2021) to valid BPMN 2.0 XML.
You understand Visio XML structure including pages, shapes, connects, and text elements.

{convertGlossary}

Use raw UTF-8 Arabic characters directly — NEVER use XML numeric entities like &#x62C; for Arabic text.{languageRule}";

        var userPrompt = $@"Convert this Visio diagram to a BPMN 2.0 collaboration diagram.

## VISIO XML CONTENT:
{truncatedContent}
{contextText}{retryBlock}

## ABSOLUTE RULE 1 — ID ALPHABET

Every `id=""...""` value is ASCII ONLY: `[A-Za-z0-9_.-]+`. Never put Arabic or any
other non-ASCII in an ID, even to make it descriptive. ALL descriptive text goes
in the `name=""...""` attribute — which IS free-form Arabic.

BAD  (will break the renderer):
  <sequenceFlow id=""Flow_بداية_إلى_تقديم"" sourceRef=""startEvent_1"" targetRef=""userTask_1"" />
  <bpmndi:BPMNEdge bpmnElement=""Flow_بداية_إلى_تقديم"" />

GOOD (ASCII id + Arabic name):
  <sequenceFlow id=""Flow_1"" name=""تقديم الطلب"" sourceRef=""startEvent_1"" targetRef=""userTask_1"" />
  <bpmndi:BPMNEdge bpmnElement=""Flow_1"" />

## ABSOLUTE RULE 2 — EVERY DI REF MUST RESOLVE

For every <bpmndi:BPMNShape bpmnElement=""X""/> and every
<bpmndi:BPMNEdge bpmnElement=""Y""/>, the values X and Y MUST EXACTLY equal the
`id` of a real element in <bpmn:collaboration> or <bpmn:process>. Do not invent
DI-side ids. Do not abbreviate. Byte-for-byte match.

## FIDELITY — THIS IS THE MOST IMPORTANT RULE

Capture **every** meaningful shape in the source. Do NOT summarize, collapse, or
simplify. If the Visio has 12 process boxes, the BPMN has 12 tasks. If the Visio
has 3 decisions, the BPMN has 3 gateways. Do NOT reduce a multi-step flow to a
'submit → review → end' skeleton — that's a failure.

Before emitting XML, mentally enumerate:
  - Every <Shape> with readable Text (excluding containers/swimlanes themselves)
  - Every <Connect FromSheet='' ToSheet=''/> relationship
  - Every container/band that behaves as a swimlane
Then emit BPMN elements 1:1 with that enumeration.

## VISIO SHAPE MAPPING:

### Shapes → BPMN element types
- **Terminator / Oval** (NameU='Terminator', 'Start/End', 'Circle', 'Ellipse') → bpmn:startEvent at the earliest position, bpmn:endEvent at terminals.
- **Process / Rectangle** (NameU='Process', 'Activity', 'Task') → bpmn:userTask by default. Promote to:
  - **bpmn:sendTask** if the action clearly dispatches a message/document to another pool ('send', 'notify', 'إرسال', 'إشعار', 'إخطار').
  - **bpmn:receiveTask** if it clearly awaits a message from another pool ('receive', 'await', 'استلام الطلب' when the pool is the recipient).
  - **bpmn:serviceTask** if the action is automated / system-driven ('generate', 'auto', 'system', 'توليد', 'نظام').
  - **bpmn:scriptTask** / **bpmn:manualTask** if the source explicitly says so.
- **Decision / Diamond** (NameU='Decision') → bpmn:exclusiveGateway. If the diamond has more than 2 outgoing branches or the text suggests parallelism, use bpmn:parallelGateway or bpmn:inclusiveGateway.
- **Intermediate circle with inner icon or ""wait""/""timer""/""انتظار""/""مؤقت"" text** → bpmn:intermediateCatchEvent with <bpmn:timerEventDefinition/> or <bpmn:messageEventDefinition/> as applicable.
- **Document shape / rolled rectangle** → bpmn:dataObjectReference AND a bpmn:association linking it to the task that produces/consumes it.
- **Cylinder / Data store** → bpmn:dataStoreReference + association.
- **Rounded rectangle with ""+"" marker or ""subprocess""** → bpmn:subProcess (collapsed).

### Containers → BPMN pools / lanes
- Top-level container band / ""Swimlane"" / ""Cross-Functional"" row → bpmn:participant (Pool).
- Sub-band within a pool → bpmn:lane inside the participant's process.
- Pools always reference their process via `processRef`.

### Connectors → BPMN flows (NON-NEGOTIABLE)
- Connector whose source AND target are inside the SAME pool → **bpmn:sequenceFlow** in that pool's process.
- Connector whose endpoints span TWO pools → **bpmn:messageFlow** inside the <bpmn:collaboration>, NOT a sequenceFlow. This is mandatory. Every cross-pool interaction in the Visio MUST produce a messageFlow with a descriptive `name` (the payload, e.g. `name=""تقديم الطلب""`, `name=""إشعار القرار""`).
- A messageFlow that LEAVES a pool implies a sendTask (or intermediateThrowEvent + messageEventDefinition) on the source side; a messageFlow that ENTERS a pool implies a receiveTask (or intermediateCatchEvent + messageEventDefinition) on the target side. Type the tasks accordingly.

### Gateway branch labels
- Every outgoing flow from an exclusiveGateway MUST have a `name` attribute describing the condition. Use bilingual vocabulary that matches the domain (موافق / مرفوض, approved / rejected, نعم / لا, yes / no).

## EXTRACTION RULES:

1. **Shape text**: <Text> children inside <Shape>. Strip Visio formatting codes.
2. **Geometry**: <Cell N='PinX' />, <Cell N='PinY' />, <Cell N='Width' />, <Cell N='Height' />. Visio origin is bottom-left; invert Y for BPMN (top-left origin).
3. **Connects**: <Connects><Connect FromSheet='' ToSheet='' FromCell='' ToCell=''/></Connects> — FromSheet/ToSheet are Shape IDs.
4. **Swimlane detection**: a Shape whose NameU contains 'Swimlane', 'Cross-Functional', 'Band', or 'Pool', OR a large container whose children live inside its bounds.

## OUTPUT REQUIREMENTS:

1. Produce a **bpmn:collaboration** whenever ≥ 2 swimlanes/pools are detectable. Single-pool diagrams are only acceptable when the Visio has zero swimlanes.
2. Every <bpmn:participant> has a real `name` (the pool label in Arabic if `languageRule` says so) and a `processRef` to an actual <bpmn:process> element.
3. Emit **bpmndi:BPMNPlane bpmnElement=""<collaboration id>""** for collaborations.
4. Every BPMN element in <bpmn:process>/<bpmn:collaboration> MUST have a corresponding <bpmndi:BPMNShape> or <bpmndi:BPMNEdge>. No orphans on either side.
5. IDs: ASCII only. Letters, digits, `_`, `-`, `.`. NEVER embed Arabic/Unicode characters in `id=""...""` — they break the viewer. Use ASCII IDs like `Task_Submit`, `Flow_Review`, `Gateway_Approval` and put Arabic text in `name=""...""` only.
6. Use these namespaces:
   - xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
   - xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI""
   - xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC""
   - xmlns:di=""http://www.omg.org/spec/DD/20100524/DI""

## LAYOUT (CRITICAL for bpmn-js rendering)

Pools are ALWAYS drawn as horizontal swim lanes — width ≫ height.

- **Pool shape**: width = 1600, height = 250 per pool. Pools stack vertically: pool 1 at y=0, pool 2 at y=260 (10px gap), etc. NEVER emit a pool that's taller than it is wide — bpmn-js will rotate the layout and make the label unreadable.
- **Shape sizes**: tasks 120×80, events 36×36, gateways 50×50. Leave ~60-80px horizontal gap between neighboring shapes so flow arrows have room.
- **Element y within pool**: centered vertically inside the pool band. For pool-height 250, tasks at y=(pool.y + 85), events at y=(pool.y + 107), gateways at y=(pool.y + 100).
- **Visio → pixels**: Visio units are inches; `px = round(inches * 96)`. If the Visio is tiny or missing coordinates, fall back to the rule above and lay elements out left-to-right in declaration order.
- **Message flow waypoints**: start at the source shape's top/bottom edge, go vertically to the target pool, then horizontally to the target shape. Never diagonal.
- **Sequence flow waypoints**: orthogonal only (horizontal or vertical segments).

## SELF-CHECK BEFORE YOU RETURN

- [ ] Every Visio shape with readable text became a BPMN element.
- [ ] Every cross-pool connector became a `<bpmn:messageFlow>` with a name.
- [ ] Every pool shape has width ≥ 4 × height.
- [ ] Every id is ASCII [A-Za-z0-9_.-], never Arabic.
- [ ] Every <bpmndi:BPMNShape>/<bpmndi:BPMNEdge> `bpmnElement` resolves to a real id.

Return ONLY the complete BPMN 2.0 XML. No markdown fences, no explanations.";

        return await InvokeOpenAIAsync(systemPrompt, userPrompt);
    }

    // Arabic Unicode block is U+0600..U+06FF (plus U+0750..U+077F, U+08A0..U+08FF
    // supplements). Simple range check on any present codepoint is enough to
    // decide "this sheet is Arabic" — we don't need a real language detector.
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

    public async Task<InitiativeScoringSuggestion> SuggestInitiativeScoringAsync(
        string titleEn, string titleAr, string? descriptionEn, string? descriptionAr,
        string? processName, string? scope)
    {
        var system = @"You are an experienced PMO analyst scoring proposed Improvement Initiatives on an Impact \u00D7 Effort matrix (1-10 each).

SCORING ANCHORS
- Impact 10: transformational strategic shift, large customer/citizen benefit, multi-year compounding return.
- Impact 5: clear measurable benefit to one team or one service line.
- Impact 1: minor cosmetic improvement, mostly internal.

- Effort 10: cross-functional programme, multi-quarter, significant external dependencies and procurement.
- Effort 5: 1\u20132 quarters with one team, modest configuration / minor tooling.
- Effort 1: one-week change a single owner can deliver.

QUADRANT MAP (impact threshold = 7, effort threshold = 4)
- Quick Win : Impact >= 7  AND Effort <= 4
- Big Bet   : Impact >= 7  AND Effort >  4
- Fill-In   : Impact <  7  AND Effort <= 4
- Thankless : Impact <  7  AND Effort >  4

OUTPUT (strict JSON, no prose around it):
{""impact"": <int 1-10>, ""effort"": <int 1-10>, ""quadrant"": ""QuickWin|BigBet|Fill-In|ThanklessTask"", ""reasoning"": ""<2-3 sentences justifying both scores>""}";

        var ar = !string.IsNullOrWhiteSpace(titleAr) ? $"\nArabic title: {titleAr}" : "";
        var descPart = !string.IsNullOrWhiteSpace(descriptionEn)
            ? $"\nDescription: {descriptionEn}"
            : (!string.IsNullOrWhiteSpace(descriptionAr) ? $"\nDescription (Arabic): {descriptionAr}" : "");
        var procPart = !string.IsNullOrWhiteSpace(processName) ? $"\nLinked process: {processName}" : "";
        var scopePart = !string.IsNullOrWhiteSpace(scope) ? $"\nScope: {scope}" : "";
        var user = $"Score this initiative.\nTitle: {titleEn}{ar}{descPart}{procPart}{scopePart}";

        var raw = await InvokeOpenAIAsync(system, user);

        // Strip ```json fences if the model added them
        var json = raw.Trim();
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            if (firstNewline > 0) json = json.Substring(firstNewline + 1);
            var fenceEnd = json.LastIndexOf("```");
            if (fenceEnd > 0) json = json.Substring(0, fenceEnd);
            json = json.Trim();
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var impact = Math.Clamp(root.TryGetProperty("impact", out var i) ? i.GetInt32() : 5, 1, 10);
            var effort = Math.Clamp(root.TryGetProperty("effort", out var e) ? e.GetInt32() : 5, 1, 10);
            var reasoning = root.TryGetProperty("reasoning", out var r) ? (r.GetString() ?? "") : "";
            var quadrant = root.TryGetProperty("quadrant", out var q) ? (q.GetString() ?? "") : "";
            if (string.IsNullOrWhiteSpace(quadrant))
            {
                // Recompute from scores if the model omitted it.
                quadrant = (impact >= 7, effort <= 4) switch
                {
                    (true,  true ) => "QuickWin",
                    (true,  false) => "BigBet",
                    (false, true ) => "Fill-In",
                    _              => "ThanklessTask"
                };
            }
            return new InitiativeScoringSuggestion
            {
                Impact = impact,
                Effort = effort,
                Reasoning = reasoning,
                Quadrant = quadrant
            };
        }
        catch
        {
            // Model returned non-JSON text \u2014 fall back to neutral midpoint with
            // the raw text as reasoning so the user at least sees something.
            return new InitiativeScoringSuggestion
            {
                Impact = 5,
                Effort = 5,
                Quadrant = "Fill-In",
                Reasoning = string.IsNullOrWhiteSpace(raw) ? "AI returned no scoreable response." : raw
            };
        }
    }
}

// Helper classes for JSON deserialization
public class ChatMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
}

public class OpenAIResponse
{
    [JsonPropertyName("choices")]
    public List<OpenAIChoice>? Choices { get; set; }
}

public class OpenAIChoice
{
    [JsonPropertyName("message")]
    public OpenAIMessage? Message { get; set; }
}

public class OpenAIMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
