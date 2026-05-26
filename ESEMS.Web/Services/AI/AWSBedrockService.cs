using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ESEMS.Web.Services.AI;

/// <summary>
/// AWS Bedrock implementation of AI service.
/// </summary>
public class AWSBedrockService : IAIService
{
    private readonly AmazonBedrockRuntimeClient _bedrockClient;
    private readonly string _modelId;
    private readonly List<string> _fallbackModelIds;
    private readonly int _maxTokens;
    private readonly double _temperature;
    private readonly ILogger<AWSBedrockService> _logger;
    private readonly string? _configurationError;

    public AWSBedrockService(IConfiguration configuration, ILogger<AWSBedrockService> logger)
    {
        _logger = logger;

        var region = configuration["AWS:Region"] ?? "us-east-1";
        var accessKey = configuration["AWS:AccessKey"];
        var secretKey = configuration["AWS:SecretKey"];
	        var profileName = configuration["AWS:Profile"]
	                          ?? Environment.GetEnvironmentVariable("AWS_PROFILE")
	                          ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_PROFILE")
	                          ?? "default";
        var useInstanceProfile = bool.TryParse(configuration["AWS:UseInstanceProfile"], out var uip) && uip;

        // Default to an AWS native Bedrock model that does not require the Anthropic use-case form.
        _modelId = configuration["AWS:Bedrock:ModelId"] ?? "amazon.nova-lite-v1:0";

        // Optional fallback list (comma/semicolon separated). Used only if the primary model is not available.
        var fallbackRaw = configuration["AWS:Bedrock:FallbackModelIds"];
        _fallbackModelIds = string.IsNullOrWhiteSpace(fallbackRaw)
            ? new List<string> { "amazon.nova-lite-v1:0", "amazon.nova-micro-v1:0" }
            : fallbackRaw
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList();

        // Ensure primary model is tried first (and not duplicated in fallback list)
        _fallbackModelIds.RemoveAll(m => string.Equals(m, _modelId, StringComparison.Ordinal));
        // TryParse — if the config value is garbage we fall back to sane
        // defaults instead of crashing the whole DI container build.
        _maxTokens = int.TryParse(
            configuration["AWS:Bedrock:MaxTokens"],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var mt) ? mt : 2000;
        _temperature = double.TryParse(
            configuration["AWS:Bedrock:Temperature"],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var t) ? t : 0.7;

        // Resolve credentials in a local-dev-friendly way.
        // IMPORTANT: We avoid falling back to EC2 Instance Metadata (IMDS) unless explicitly enabled,
        // because local machines are not on EC2 and would otherwise throw IMDS errors.
	        // Prevent confusing local-dev failures where the SDK tries to hit EC2 IMDS.
	        // If you *do* want IMDS (EC2 instance role), enable AWS:UseInstanceProfile=true.
	        if (!useInstanceProfile && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_EC2_METADATA_DISABLED")))
	            Environment.SetEnvironmentVariable("AWS_EC2_METADATA_DISABLED", "true");

	        var (credentials, error) = ResolveCredentials(accessKey, secretKey, profileName, useInstanceProfile);
        _configurationError = error;
        credentials ??= new AnonymousAWSCredentials();

        // Create Bedrock Runtime client
        _bedrockClient = new AmazonBedrockRuntimeClient(credentials, RegionEndpoint.GetBySystemName(region));
    }

    private static (AWSCredentials? Credentials, string? Error) ResolveCredentials(
        string? accessKey,
        string? secretKey,
        string profileName,
        bool useInstanceProfile)
    {
        // 1) Explicit keys from config
        if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
            return (new BasicAWSCredentials(accessKey.Trim(), secretKey.Trim()), null);

        // 2) Environment variables
        var envAccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var envSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        if (!string.IsNullOrWhiteSpace(envAccessKey) && !string.IsNullOrWhiteSpace(envSecretKey))
            return (new EnvironmentVariablesAWSCredentials(), null);

	        // 3) Shared AWS config/credentials profile (aws configure / aws sso login)
	        try
	        {
	            var chain = new CredentialProfileStoreChain();
	            if (!string.IsNullOrWhiteSpace(profileName) && chain.TryGetAWSCredentials(profileName, out var profileCreds))
	                return (profileCreds, null);
	        }
	        catch
	        {
	            // ignore and keep going
	        }

	        // 4) Default SDK credential chain (supports more sources such as SSO/credential_process)
	        // Note: EC2 IMDS is disabled in local dev unless AWS:UseInstanceProfile=true.
	        try
	        {
	            var creds = FallbackCredentialsFactory.GetCredentials();
	            _ = creds.GetCredentials();
	            return (creds, null);
	        }
	        catch
	        {
	            // ignore and keep going
	        }

	        // 5) Optional: EC2 instance profile (IMDS) for EC2 deployments only
	        if (useInstanceProfile)
	            return (new InstanceProfileAWSCredentials(), null);

	        return (null,
	            $"AWS credentials not found (tried profile '{profileName}'). Configure one of: " +
            "(1) appsettings: AWS:AccessKey + AWS:SecretKey (local only), " +
            "(2) environment: AWS_ACCESS_KEY_ID + AWS_SECRET_ACCESS_KEY, " +
            "(3) AWS CLI profile via `aws configure` (then set AWS:Profile or AWS_PROFILE if needed). " +
            "If deploying on EC2 with IAM role, set AWS:UseInstanceProfile=true.");
    }

    public async Task<string> GenerateProcessImprovementSuggestionsAsync(string processName, string processDescription, string? currentIssues = null)
    {
        var prompt = $@"You are an expert in business process improvement and operational excellence.

Process Name: {processName}
Process Description: {processDescription}
{(string.IsNullOrEmpty(currentIssues) ? "" : $"Current Issues: {currentIssues}")}

Please provide:
1. 3-5 specific improvement suggestions
2. Expected benefits for each suggestion
3. Implementation difficulty (Low/Medium/High)
4. Estimated impact (Low/Medium/High)

Format your response in a clear, structured way.";

        return await InvokeClaudeAsync(prompt);
    }

    public async Task<string> AnalyzeRiskAndSuggestMitigationAsync(string riskDescription, string riskCategory, int likelihood, int impact)
    {
        var prompt = $@"You are a risk management expert.

Risk Description: {riskDescription}
Risk Category: {riskCategory}
Likelihood (1-10): {likelihood}
Impact (1-10): {impact}

Please provide:
1. Risk analysis and severity assessment
2. 3-5 specific mitigation strategies
3. Preventive measures
4. Contingency plans

Format your response in a clear, structured way.";

        return await InvokeClaudeAsync(prompt);
    }

    public async Task<string> GenerateRACISuggestionsAsync(string activityName, string activityDescription, string organizationContext)
    {
        var prompt = $@"You are an organizational design expert.

Activity Name: {activityName}
Activity Description: {activityDescription}
Organization Context: {organizationContext}

Please suggest a RACI matrix (Responsible, Accountable, Consulted, Informed) for this activity.
Provide role suggestions and explain the reasoning for each assignment.

Format your response in a clear, structured way.";

        return await InvokeClaudeAsync(prompt);
    }

    public async Task<string> SummarizeAuditLogsAsync(List<string> auditLogEntries, string timeframe)
    {
        var logsText = string.Join("\n", auditLogEntries.Take(100)); // Limit to avoid token overflow

        var prompt = $@"You are a data analyst specializing in audit log analysis.

Timeframe: {timeframe}
Audit Log Entries:
{logsText}

Please provide:
1. Summary of key activities
2. Notable patterns or anomalies
3. User activity highlights
4. Recommendations for security or compliance

Format your response in a clear, structured way.";

        return await InvokeClaudeAsync(prompt);
    }

    public async Task<string> GenerateStrategicObjectiveRecommendationsAsync(string organizationGoals, string currentObjectives)
    {
        var prompt = $@"You are a strategic planning consultant.

Organization Goals: {organizationGoals}
Current Objectives: {currentObjectives}

Please provide:
1. Analysis of current objectives alignment with goals
2. 3-5 new strategic objective recommendations
3. Key performance indicators (KPIs) for each objective
4. Implementation priorities

Format your response in a clear, structured way.";

        return await InvokeClaudeAsync(prompt);
    }

    public async Task<string> AnalyzeServicePerformanceAsync(string serviceName, decimal? satisfactionScore, int? transactionCount, string? issues)
    {
        var prompt = $@"You are a service management expert.

Service Name: {serviceName}
Customer Satisfaction Score: {satisfactionScore?.ToString() ?? "N/A"}
Annual Transaction Count: {transactionCount?.ToString() ?? "N/A"}
{(string.IsNullOrEmpty(issues) ? "" : $"Known Issues: {issues}")}

Please provide:
1. Performance analysis
2. Areas for improvement
3. Specific recommendations
4. Best practices to implement

Format your response in a clear, structured way.";

        return await InvokeClaudeAsync(prompt);
    }

    public async Task<(string English, string Arabic)> GenerateBilingualContentAsync(string prompt, string context)
    {
        var bilingualPrompt = $@"You are a bilingual content generator fluent in English and Arabic.

Context: {context}
Request: {prompt}

Please provide the content in both languages:
1. First in English
2. Then in Arabic

Format:
ENGLISH:
[English content here]

ARABIC:
[Arabic content here]";

        var response = await InvokeClaudeAsync(bilingualPrompt);

        // Parse the response to extract English and Arabic parts
        var parts = response.Split(new[] { "ENGLISH:", "ARABIC:" }, StringSplitOptions.RemoveEmptyEntries);

        string english = parts.Length > 0 ? parts[0].Trim() : response;
        string arabic = parts.Length > 1 ? parts[1].Trim() : "";

        return (english, arabic);
    }

    public async Task<string> ChatAsync(string userMessage, List<(string role, string content)>? conversationHistory = null)
    {
        if (!string.IsNullOrWhiteSpace(_configurationError))
            return $"Error: {_configurationError}";

        var systemPrompt = @"You are an AI assistant for an Environmental and Safety Excellence Management System (ESEMS).
You help users with:
- Process management and improvement
- Risk assessment and mitigation
- Strategic planning
- Service optimization
- Compliance and audit support
- General business process questions

Provide helpful, accurate, and actionable advice.";

        var messages = new List<Message>();

        if (conversationHistory != null && conversationHistory.Any())
        {
            foreach (var (role, content) in conversationHistory)
            {
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                messages.Add(new Message
                {
                    Role = MapConversationRole(role),
                    Content = new List<ContentBlock> { new ContentBlock { Text = content } }
                });
            }
        }

        // Current user message
        messages.Add(new Message
        {
            Role = ConversationRole.User,
            Content = new List<ContentBlock> { new ContentBlock { Text = userMessage } }
        });

        try
        {
            return await InvokeConverseAsync(messages, systemPrompt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking AWS Bedrock Converse API");
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Core method to invoke the configured Bedrock model.
    /// For Titan text models we keep the legacy InvokeModel payload.
    /// For other models we use Bedrock Converse (model-agnostic chat API).
    /// </summary>
    private async Task<string> InvokeClaudeAsync(string prompt)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_configurationError))
                return $"Error: {_configurationError}";

            // Check if using Amazon Titan
            if (_modelId.StartsWith("amazon.titan"))
            {
                return await InvokeTitanAsync(prompt);
            }

            // One-shot user prompt via Converse
            var messages = new List<Message>
            {
                new Message
                {
                    Role = ConversationRole.User,
                    Content = new List<ContentBlock> { new ContentBlock { Text = prompt } }
                }
            };

            return await InvokeConverseAsync(messages, systemPrompt: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking AWS Bedrock model");
            return $"Error: {ex.Message}";
        }
    }

    private async Task<string> InvokeConverseAsync(List<Message> messages, string? systemPrompt)
    {
        try
        {
            return await InvokeConverseWithModelAsync(_modelId, messages, systemPrompt);
        }
        catch (AmazonServiceException ex) when (ShouldTryFallback(ex))
        {
            _logger.LogWarning(ex, "Primary Bedrock model '{ModelId}' failed; trying fallbacks.", _modelId);

            foreach (var fallbackModelId in _fallbackModelIds)
            {
                try
                {
                    _logger.LogInformation("Trying Bedrock fallback model '{ModelId}'.", fallbackModelId);
                    return await InvokeConverseWithModelAsync(fallbackModelId, messages, systemPrompt);
                }
                catch (AmazonServiceException fallbackEx) when (ShouldTryFallback(fallbackEx))
                {
                    _logger.LogWarning(fallbackEx, "Bedrock fallback model '{ModelId}' failed.", fallbackModelId);
                    continue;
                }
            }

            throw;
        }
    }

    private async Task<string> InvokeConverseWithModelAsync(string modelId, List<Message> messages, string? systemPrompt)
    {
        if (!string.IsNullOrWhiteSpace(_configurationError))
            return $"Error: {_configurationError}";

        var request = new ConverseRequest
        {
            ModelId = modelId,
            Messages = messages,
            InferenceConfig = new InferenceConfiguration
            {
                MaxTokens = _maxTokens,
                Temperature = (float)_temperature,
                TopP = 0.9F
            }
        };

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            request.System = new List<SystemContentBlock>
            {
                new SystemContentBlock { Text = systemPrompt }
            };
        }

        var response = await _bedrockClient.ConverseAsync(request);
        return ExtractText(response);
    }

    private static bool ShouldTryFallback(AmazonServiceException ex)
    {
        // Keep this conservative: only fallback when the error is likely model-specific.
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("use case", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("details have not been submitted", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("don't have access", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("do not have access", StringComparison.OrdinalIgnoreCase)
               || (msg.Contains("model", StringComparison.OrdinalIgnoreCase) && msg.Contains("not found", StringComparison.OrdinalIgnoreCase))
               || msg.Contains("end of its life", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractText(ConverseResponse response)
    {
        var blocks = response.Output?.Message?.Content;
        if (blocks == null || blocks.Count == 0)
            return "No response generated.";

        var sb = new StringBuilder();
        foreach (var block in blocks)
        {
            if (!string.IsNullOrEmpty(block.Text))
                sb.Append(block.Text);
        }

        var text = sb.ToString();
        return string.IsNullOrWhiteSpace(text) ? "No response generated." : text;
    }

    private static ConversationRole MapConversationRole(string role)
    {
        var normalized = role?.Trim().ToLowerInvariant();
        return normalized == "assistant" ? ConversationRole.Assistant : ConversationRole.User;
    }

    private async Task<string> InvokeTitanAsync(string prompt)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_configurationError))
                return $"Error: {_configurationError}";

            var requestBody = new
            {
                inputText = prompt,
                textGenerationConfig = new
                {
                    maxTokenCount = _maxTokens,
                    temperature = _temperature,
                    topP = 0.9
                }
            };

            var jsonRequest = JsonSerializer.Serialize(requestBody);

            var request = new InvokeModelRequest
            {
                ModelId = _modelId,
                Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonRequest)),
                ContentType = "application/json",
                Accept = "application/json"
            };

            var response = await _bedrockClient.InvokeModelAsync(request);

            using var reader = new StreamReader(response.Body);
            var responseBody = await reader.ReadToEndAsync();

            var jsonResponse = JsonDocument.Parse(responseBody);
            var results = jsonResponse.RootElement.GetProperty("results");

            if (results.GetArrayLength() > 0)
            {
                var outputText = results[0].GetProperty("outputText").GetString();
                return outputText ?? "No response generated.";
            }

            return "No response generated.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking Amazon Titan model");
            return $"Error: {ex.Message}";
        }
    }

	public async Task<string> GenerateBPMNDiagramAsync(string processName, string processDescription, List<string>? steps = null, bool? hintArabic = null)
    {
        // hintArabic reserved for callers that know the target language; the
        // Bedrock prompt below is currently English-only so the flag is
        // accepted for interface parity but not yet consumed.
        _ = hintArabic;
        var stepsText = steps != null && steps.Any()
            ? $"\nProcess Steps:\n{string.Join("\n", steps.Select((s, i) => $"{i + 1}. {s}"))}"
            : "";

		var prompt = $@"You are an expert BPMN 2.0 modeler. Generate a BPMN 2.0 XML diagram for bpmn-js.

Process Name: {processName}
Process Description: {processDescription}{stepsText}

## ARCHITECTURE — SINGLE POOL, NO LANES
- Place ALL elements in ONE <bpmn:process> inside ONE <bpmn:participant> pool.
- Do NOT use lanes or multiple pools — they cause rendering issues.
- Label each task with the actor, e.g. ""Customer: Submit Application"", ""Manager: Approve Request"".

## PROCESS FLOW (MANDATORY)
1. Exactly 1 <bpmn:startEvent>
2. Then 5-8 sequential activities (<bpmn:userTask> for human work, <bpmn:serviceTask> for automated)
3. Use <bpmn:exclusiveGateway> for decisions — EVERY gateway MUST have exactly 2 outgoing <bpmn:sequenceFlow> with name=""Yes""/""No"" or ""Approved""/""Rejected""
4. Both gateway branches MUST reach a <bpmn:endEvent>
5. End with 1-2 <bpmn:endEvent> elements
6. Connect ALL elements with <bpmn:sequenceFlow> — zero orphan elements

## LAYOUT (Critical — follow exactly)
- Pool: x=50, y=50, width=1600, height=300
- Happy path elements on y=160 (center of pool)
- Rejected/failed branch on y=280
- X spacing: startEvent at x=160, then +180px each: 340, 520, 700, 880, 1060, 1240, 1420
- Sizes: Tasks width=""100"" height=""80"" | Events width=""36"" height=""36"" | Gateways width=""50"" height=""50""
- BPMNEdge waypoints: source RIGHT edge → target LEFT edge. Example for task(x=340,w=100,h=80) to task(x=520,w=100,h=80): waypoint x=""440"" y=""200"" then x=""520"" y=""200""
- Gateway branch down: waypoint at (gateway_x+25, 160) → (gateway_x+25, 280) → (target_x, 280+h/2)

## XML STRUCTURE (follow this skeleton exactly)
```
<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL""
  xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI""
  xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC""
  xmlns:di=""http://www.omg.org/spec/DD/20100524/DI""
  id=""Definitions_1"" targetNamespace=""http://bpmn.io/schema/bpmn"">
  <bpmn:collaboration id=""Collaboration_1"">
    <bpmn:participant id=""Participant_1"" name=""POOL_NAME"" processRef=""Process_1"" />
  </bpmn:collaboration>
  <bpmn:process id=""Process_1"" isExecutable=""false"">
    <bpmn:startEvent id=""Start_1"" name=""Start"">
      <bpmn:outgoing>Flow_1</bpmn:outgoing>
    </bpmn:startEvent>
    <!-- tasks, gateways, endEvents here with <bpmn:incoming> and <bpmn:outgoing> -->
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""Start_1"" targetRef=""Task_1"" />
    <!-- all sequenceFlows here -->
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Collaboration_1"">
      <bpmndi:BPMNShape id=""Participant_1_di"" bpmnElement=""Participant_1"" isHorizontal=""true"">
        <dc:Bounds x=""50"" y=""50"" width=""1600"" height=""300"" />
      </bpmndi:BPMNShape>
      <!-- BPMNShape for every element, BPMNEdge for every flow -->
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>
```

## VALIDATION CHECKLIST (verify before returning)
- Every element has a BPMNShape with dc:Bounds
- Every sequenceFlow has a BPMNEdge with di:waypoint elements
- Every gateway has exactly 2 outgoing sequenceFlows with name attributes
- All sourceRef/targetRef reference existing element IDs
- Every task/event has correct <bpmn:incoming> and <bpmn:outgoing> elements
- No orphan elements — everything connected

Return ONLY the complete BPMN 2.0 XML. No markdown fences, no explanations, no commentary.";

        return await InvokeClaudeAsync(prompt);
    }

	public async Task<string> RefineBPMNDiagramAsync(string currentBpmnXml, string updateInstructions)
    {
		var prompt = $@"You are a BPMN 2.0 expert. Update the BPMN XML below according to the user's instructions.

## CURRENT BPMN XML:
{currentBpmnXml}

## USER INSTRUCTIONS:
{updateInstructions}

## RULES:
1. Return ONLY the complete updated BPMN 2.0 XML — no markdown fences, no explanations.
2. Preserve ALL existing element IDs that are unchanged.
3. For NEW elements: add BPMNShape with dc:Bounds and proper <bpmn:incoming>/<bpmn:outgoing>.
4. For REMOVED elements: also remove their BPMNShape/BPMNEdge and update incoming/outgoing refs.
5. Recalculate BPMNEdge waypoints for any moved or new elements.
6. Maintain layout: happy path y=160, alternate path y=280, x spacing +180px.
7. Every gateway must have exactly 2 named outgoing flows.
8. All sourceRef/targetRef must reference existing element IDs.
9. Pool width should accommodate all elements (last element x + 200).

Return ONLY BPMN XML.";

		return await InvokeClaudeAsync(prompt);
    }

    public async Task<string> ConvertVisioToBPMNAsync(string visioXmlContent, string? additionalContext = null, IReadOnlyList<string>? previousErrors = null)
    {
        // Truncate very large Visio content to avoid token limits
        var truncatedContent = visioXmlContent.Length > 15000
            ? visioXmlContent.Substring(0, 15000) + "\n... (truncated)"
            : visioXmlContent;

        var contextText = !string.IsNullOrWhiteSpace(additionalContext)
            ? $"\nAdditional Context: {additionalContext}"
            : "";

        var retryBlock = previousErrors is { Count: > 0 }
            ? $@"

RETRY: your previous attempt failed validation with these errors:
{string.Join("\n", previousErrors.Take(8).Select(e => "  - " + e))}

Produce a new BPMN XML that fixes every error above. Pay particular attention to:
  - sequenceFlow sourceRef/targetRef MUST match an existing element id
  - every <bpmndi:BPMNShape bpmnElement='x'/> and <bpmndi:BPMNEdge bpmnElement='x'/> MUST reference an id that exists in the <bpmn:process>
"
            : "";

        var prompt = $@"Convert the following Visio diagram XML to valid BPMN 2.0 XML that bpmn-js can render.

Visio Content:
{truncatedContent}
{contextText}{retryBlock}

Hard requirements (each one is a common failure point — check yourself):
1. Exactly one <bpmn:definitions> root with the standard BPMN/DC/DI namespaces.
2. Exactly one <bpmn:process> containing every flowNode and sequenceFlow.
3. Every element has a unique id. Ids use the forms: StartEvent_N, EndEvent_N, Task_N, Gateway_N, Flow_N.
4. Every <bpmn:sequenceFlow> carries sourceRef AND targetRef, each pointing to an id that exists in the process.
5. Emit a <bpmndi:BPMNDiagram> with a <bpmndi:BPMNPlane bpmnElement='{{process id}}'>. For every flowNode emit a <bpmndi:BPMNShape bpmnElement='{{node id}}'> with <dc:Bounds>. For every sequenceFlow emit a <bpmndi:BPMNEdge bpmnElement='{{flow id}}'> with at least two <di:waypoint>.
6. Every BPMNShape/BPMNEdge bpmnElement attribute must match an id in the process (no dangling refs).
7. Map shapes by intent: ovals → startEvent/endEvent; diamonds → exclusiveGateway (or parallelGateway for AND splits, inclusiveGateway for OR); arrows → sequenceFlow. Preserve arrow directions.
7a. Rectangles → classify by the label's verb:
    - <userTask> when a human performs the step: review, approve, decide, sign, fill, enter, submit, verify, assign, inspect, confirm, reject (Arabic equivalents: مراجعة، اعتماد، توقيع، تعبئة، إدخال، تقديم، موافقة، رفض).
    - <serviceTask> when an automated system does it: send email, notify, generate report/PDF, call API, query database, integrate, sync, export (Arabic: إرسال، إشعار، توليد، تصدير، مزامنة).
    - <manualTask> for non-digital physical steps (e.g. archive paper file, hand document to courier).
    - generic <task> only when intent is genuinely ambiguous.
8. If Visio has swimlanes, add a <bpmn:laneSet> with one <bpmn:lane> per swimlane and <bpmn:flowNodeRef> entries for the nodes in that lane.

Reference structure (study the id cross-references; your output must have the same integrity):
<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:bpmndi='http://www.omg.org/spec/BPMN/20100524/DI' xmlns:dc='http://www.omg.org/spec/DD/20100524/DC' xmlns:di='http://www.omg.org/spec/DD/20100524/DI' targetNamespace='http://esems/bpmn'>
  <bpmn:process id='Process_1' isExecutable='false'>
    <bpmn:startEvent id='StartEvent_1' name='Start'/>
    <bpmn:task id='Task_1' name='Review'/>
    <bpmn:exclusiveGateway id='Gateway_1' name='Approved?'/>
    <bpmn:endEvent id='EndEvent_1' name='Done'/>
    <bpmn:sequenceFlow id='Flow_1' sourceRef='StartEvent_1' targetRef='Task_1'/>
    <bpmn:sequenceFlow id='Flow_2' sourceRef='Task_1' targetRef='Gateway_1'/>
    <bpmn:sequenceFlow id='Flow_3' sourceRef='Gateway_1' targetRef='EndEvent_1' name='Yes'/>
  </bpmn:process>
  <bpmndi:BPMNDiagram id='D_1'><bpmndi:BPMNPlane id='P_1' bpmnElement='Process_1'>
    <bpmndi:BPMNShape id='S1' bpmnElement='StartEvent_1'><dc:Bounds x='100' y='100' width='36' height='36'/></bpmndi:BPMNShape>
    <bpmndi:BPMNShape id='S2' bpmnElement='Task_1'><dc:Bounds x='200' y='80' width='100' height='80'/></bpmndi:BPMNShape>
    <bpmndi:BPMNShape id='S3' bpmnElement='Gateway_1'><dc:Bounds x='360' y='95' width='50' height='50'/></bpmndi:BPMNShape>
    <bpmndi:BPMNShape id='S4' bpmnElement='EndEvent_1'><dc:Bounds x='470' y='102' width='36' height='36'/></bpmndi:BPMNShape>
    <bpmndi:BPMNEdge id='E1' bpmnElement='Flow_1'><di:waypoint x='136' y='118'/><di:waypoint x='200' y='120'/></bpmndi:BPMNEdge>
    <bpmndi:BPMNEdge id='E2' bpmnElement='Flow_2'><di:waypoint x='300' y='120'/><di:waypoint x='360' y='120'/></bpmndi:BPMNEdge>
    <bpmndi:BPMNEdge id='E3' bpmnElement='Flow_3'><di:waypoint x='410' y='120'/><di:waypoint x='470' y='120'/></bpmndi:BPMNEdge>
  </bpmndi:BPMNPlane></bpmndi:BPMNDiagram>
</bpmn:definitions>

Return ONLY the BPMN XML. No markdown fences, no prose.";

        return await InvokeClaudeAsync(prompt);
    }

    public async Task<InitiativeScoringSuggestion> SuggestInitiativeScoringAsync(
        string titleEn, string titleAr, string? descriptionEn, string? descriptionAr,
        string? processName, string? scope)
    {
        var ar = !string.IsNullOrWhiteSpace(titleAr) ? $"\nArabic title: {titleAr}" : "";
        var descPart = !string.IsNullOrWhiteSpace(descriptionEn)
            ? $"\nDescription: {descriptionEn}"
            : (!string.IsNullOrWhiteSpace(descriptionAr) ? $"\nDescription (Arabic): {descriptionAr}" : "");
        var procPart = !string.IsNullOrWhiteSpace(processName) ? $"\nLinked process: {processName}" : "";
        var scopePart = !string.IsNullOrWhiteSpace(scope) ? $"\nScope: {scope}" : "";

        var prompt = $@"You are an experienced PMO analyst scoring proposed Improvement Initiatives on an Impact × Effort matrix (1-10 each).

Anchors:
- Impact 10 = transformational; 5 = clear team-level benefit; 1 = minor cosmetic.
- Effort 10 = cross-functional multi-quarter programme; 5 = 1-2 quarters one team; 1 = one-week single owner.

Quadrant map (impact threshold 7, effort threshold 4):
- Quick Win: Impact >= 7 AND Effort <= 4
- Big Bet: Impact >= 7 AND Effort > 4
- Fill-In: Impact < 7 AND Effort <= 4
- Thankless: Impact < 7 AND Effort > 4

Initiative:
Title: {titleEn}{ar}{descPart}{procPart}{scopePart}

Return ONLY this strict JSON (no markdown, no prose around it):
{{""impact"": <int 1-10>, ""effort"": <int 1-10>, ""quadrant"": ""QuickWin|BigBet|Fill-In|ThanklessTask"", ""reasoning"": ""<2-3 sentences justifying both scores>""}}";

        var raw = await InvokeClaudeAsync(prompt);
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
