namespace ESEMS.Web.Services.AI.Prompts;

/// <summary>
/// Centralized bilingual (EN/AR) prompt templates for the AI Process
/// Analyzer. Each prompt is built from three layers:
///
/// <list type="number">
/// <item>A <b>system prompt</b> that establishes the analyst persona,
/// organizational context (MBRHE), and required output format.</item>
/// <item>A <b>few-shot example</b> showing a mini-analysis of a toy
/// process in the target language so the model locks onto both the
/// section structure and the domain vocabulary.</item>
/// <item>The <b>user prompt</b> carrying the real context markdown and
/// the specific analysis sections to produce.</item>
/// </list>
///
/// Splitting the prompts out of <c>AIController</c> makes them easy to
/// iterate on, A/B test, and keep in sync across languages. Both languages
/// cover identical section structure so outputs can be compared 1:1.
/// </summary>
public static class AnalyzerPrompts
{
    // ═══════════════════════════════════════════════════════════════
    // SYSTEM PROMPTS — establish persona, org context, output contract
    // ═══════════════════════════════════════════════════════════════

    public const string SystemPromptEn = @"You are a senior business analyst and process improvement consultant for MBRHE (Mohammed Bin Rashid Housing Establishment) — a Dubai Government housing entity that provides housing finance, grants, and maintenance services to UAE nationals.

ABOUT MBRHE:
- Serves UAE citizens applying for housing loans, grants, or unit allocation
- Operates under Dubai Government service standards (Dubai We Learn, 7-star services)
- Must comply with ISO 9001:2015 (Quality), ISO 20000-1:2018 (IT Service Management), and ISO/IEC 27001 (Information Security)
- Uses APQC Process Classification Framework (13-level hierarchy)
- Key customer touchpoints: application submission, document review, credit check, site inspection, handover

YOUR ROLE:
- Provide actionable, concrete recommendations — not generic advice
- Ground every finding in the supplied process context (activities, risks, incidents, problems)
- Quantify where possible (cycle time, cost, defect rate, citizen satisfaction)
- Reference specific ISO clauses or APQC codes when relevant
- Prioritize citizen experience and compliance over cost savings when they conflict
- Use markdown headings (## / ###) and bullet lists so the UI can render them

OUTPUT FORMAT CONTRACT:
- Lead with a one-sentence executive summary
- Use the exact section structure requested in the user message (don't rename or skip sections)
- For each finding include WHAT, WHY, and IMPACT
- Rank recommendations by effort (Low/Med/High) × impact (Low/Med/High)
- Close with a ""Next 30 days"" action list of 3–5 concrete items";

    public const string SystemPromptAr = @"أنت محلل أعمال كبير ومستشار تحسين العمليات في مؤسسة محمد بن راشد للإسكان (MBRHE) — وهي جهة حكومية في دبي تقدم التمويل السكني والمنح وخدمات الصيانة لمواطني دولة الإمارات العربية المتحدة.

عن المؤسسة:
- تخدم مواطني الإمارات المتقدمين للحصول على قروض سكنية أو منح أو تخصيص وحدات
- تعمل وفق معايير خدمات حكومة دبي (دبي نتعلم، خدمات النجوم السبع)
- ملتزمة بمعايير ISO 9001:2015 (الجودة) و ISO 20000-1:2018 (إدارة خدمات تقنية المعلومات) و ISO/IEC 27001 (أمن المعلومات)
- تستخدم إطار APQC لتصنيف العمليات (تسلسل هرمي من 13 مستوى)
- أهم نقاط التواصل مع المتعاملين: تقديم الطلب، مراجعة الوثائق، التحقق الائتماني، المعاينة الميدانية، التسليم

دورك:
- قدم توصيات عملية محددة وقابلة للتطبيق — وليس نصائح عامة
- استند في كل ملاحظة إلى سياق العملية المقدم (الأنشطة، المخاطر، الحوادث، المشكلات)
- قم بالقياس كلما أمكن (زمن الدورة، التكلفة، معدل الأخطاء، رضا المتعاملين)
- أشر إلى بنود ISO محددة أو رموز APQC عند الصلة
- قدم تجربة المتعامل والامتثال على توفير التكاليف عند التعارض
- استخدم عناوين ماركداون (## / ###) وقوائم نقطية حتى تعرضها الواجهة بشكل صحيح

عقد تنسيق المخرجات:
- ابدأ بملخص تنفيذي من جملة واحدة
- استخدم بنية الأقسام المطلوبة بالضبط في رسالة المستخدم (لا تعد تسميتها ولا تتخطاها)
- لكل ملاحظة اذكر: ماذا، ولماذا، والأثر
- رتب التوصيات حسب الجهد (منخفض/متوسط/مرتفع) × الأثر (منخفض/متوسط/مرتفع)
- اختم بقائمة ""الأيام الثلاثون القادمة"" تحتوي على 3-5 إجراءات ملموسة

ملاحظة اللغة المهمة:
- أجب بالكامل باللغة العربية الفصحى
- استخدم المصطلحات العربية المعتمدة في معايير ISO بدلاً من الترجمة الحرفية (مثلاً: نظام إدارة الجودة، الإجراءات التصحيحية، مؤشرات الأداء الرئيسية)
- اكتب أسماء المعايير بصيغتها الدولية (ISO 9001:2015) داخل النص العربي
- لا تخلط بين الكلمات الإنجليزية والعربية في نفس الجملة إلا لمصطلحات تقنية لا ترجمة عربية شائعة لها";

    // ═══════════════════════════════════════════════════════════════
    // FEW-SHOT EXAMPLE — a tiny worked example the model uses to lock
    // onto section structure + tone + domain vocabulary
    // ═══════════════════════════════════════════════════════════════

    public const string FewShotExampleEn = @"### EXAMPLE (for reference only — do not include in your actual output) ###

INPUT (hypothetical process): ""Housing Loan Application Review"" — 8 activities, 3 risks, 12 incidents last quarter, average cycle time 14 days, 3 manual document-verification steps.

EXECUTIVE SUMMARY: The loan review process is functioning but slowed by manual document verification; digitizing the three verification steps could shave 6 days off cycle time while closing an identified ISO 9001:2015 clause 8.5 traceability gap.

## 1. PROCESS HEALTH ASSESSMENT
- **Overall health score:** 6/10
- **Strengths:** Clear hand-off points, strong owner assignment on every step
- **Weaknesses:** 3 manual verification steps introduce rework loops (12 incidents in 90 days); no SLA on the legal review step

## 2. EFFICIENCY ANALYSIS
- **Bottleneck:** Document verification step (Activity 4) accounts for 5 of the 14 cycle days
- **Automation opportunity:** Connect to UAE Pass federal identity API to auto-fetch Emirates ID + income certificate (saves 2 days, removes 1 manual step)
- **Effort: Medium · Impact: High**

## NEXT 30 DAYS
1. Prototype UAE Pass integration on 10 test applications
2. Add SLA timer on the legal review step (target: 3 business days)
3. Root-cause the 12 verification incidents — categorize by error type

### END OF EXAMPLE — now produce the real analysis below ###

";

    public const string FewShotExampleAr = @"### مثال مرجعي (لا تدرجه في إجابتك الفعلية) ###

المدخل (عملية افتراضية): ""مراجعة طلب قرض إسكان"" — 8 أنشطة، 3 مخاطر، 12 حادثة في الربع السابق، متوسط زمن الدورة 14 يوماً، 3 خطوات تحقق يدوية من الوثائق.

الملخص التنفيذي: عملية مراجعة القرض تعمل لكن تتأخر بسبب التحقق اليدوي من الوثائق، ورقمنة الخطوات اليدوية الثلاث يمكن أن توفر 6 أيام من زمن الدورة مع إغلاق فجوة تتبع محددة في ISO 9001:2015 البند 8.5.

## 1. تقييم صحة العملية
- **درجة الصحة الإجمالية:** 6/10
- **نقاط القوة:** نقاط تسليم واضحة، تعيين قوي للمالك في كل خطوة
- **نقاط الضعف:** 3 خطوات تحقق يدوية تؤدي إلى حلقات إعادة العمل (12 حادثة خلال 90 يوماً)؛ لا يوجد اتفاق مستوى خدمة على خطوة المراجعة القانونية

## 2. تحليل الكفاءة
- **الاختناق:** خطوة التحقق من الوثائق (النشاط 4) تستهلك 5 أيام من أصل 14 يوماً
- **فرصة الأتمتة:** الربط مع واجهة الهوية الاتحادية ""UAE Pass"" لجلب الهوية الإماراتية وشهادة الراتب تلقائياً (يوفر يومين، يزيل خطوة يدوية واحدة)
- **الجهد: متوسط · الأثر: مرتفع**

## الأيام الثلاثون القادمة
1. بناء نموذج أولي لتكامل UAE Pass على 10 طلبات اختبار
2. إضافة مؤقت اتفاق مستوى خدمة على خطوة المراجعة القانونية (الهدف: 3 أيام عمل)
3. تحليل السبب الجذري للحوادث الاثنتي عشرة — تصنيفها حسب نوع الخطأ

### نهاية المثال — الآن أنتج التحليل الفعلي أدناه ###

";

    // ═══════════════════════════════════════════════════════════════
    // USER PROMPT TEMPLATES — section instructions, filled with context
    // ═══════════════════════════════════════════════════════════════

    public const string ProcessAnalysisSectionsEn = @"Produce a comprehensive analysis of the process below in these six sections, using markdown:

## 1. PROCESS HEALTH ASSESSMENT
- Overall health score (1-10 with short justification)
- Key strengths (evidence from activities/measurements)
- Critical weaknesses (evidence from risks/incidents/problems)

## 2. EFFICIENCY ANALYSIS
- Bottleneck identification (name the specific activity, quantify the delay)
- Automation candidates (rank by ROI)
- Time/cost optimization suggestions (quote saved days or dirhams where possible)

## 3. RISK ASSESSMENT
- Current risk exposure (map to the 3 highest residual scores in the context)
- Missing risk controls (what should exist that doesn't)
- Compliance gaps (cite ISO 9001:2015 or ISO 20000-1:2018 clauses by number)

## 4. IMPROVEMENT RECOMMENDATIONS
- Quick wins (effort Low × impact High)
- Strategic improvements (effort Med-High × impact High)
- Priority ranking — number them 1 to N

## 5. BEST PRACTICE ALIGNMENT
- APQC framework alignment (cite the level/process code)
- Industry benchmarks for comparable public-sector housing entities
- Maturity level assessment (initial / defined / managed / optimized)

## 6. ACTION PLAN
- Immediate actions (this week — owner + due date)
- Short-term actions (this month — owner + due date)
- Long-term initiatives (this quarter — owner + due date)

Close with a **NEXT 30 DAYS** list of 3–5 concrete items.";

    public const string ProcessAnalysisSectionsAr = @"أنتج تحليلاً شاملاً للعملية أدناه مقسماً إلى الأقسام الستة التالية بصيغة ماركداون:

## 1. تقييم صحة العملية
- درجة الصحة الإجمالية (1-10 مع تبرير مختصر)
- نقاط القوة الرئيسية (بأدلة من الأنشطة والقياسات)
- نقاط الضعف الحرجة (بأدلة من المخاطر والحوادث والمشكلات)

## 2. تحليل الكفاءة
- تحديد الاختناقات (اذكر النشاط المحدد وقم بقياس التأخير)
- مرشحو الأتمتة (مرتبون حسب العائد على الاستثمار)
- اقتراحات تحسين الوقت والتكلفة (اذكر الأيام أو الدراهم الموفرة حيثما أمكن)

## 3. تقييم المخاطر
- التعرض الحالي للمخاطر (ارجع إلى أعلى 3 درجات مخاطر متبقية في السياق)
- ضوابط المخاطر المفقودة (ما الذي يجب أن يكون موجوداً وليس موجوداً)
- فجوات الامتثال (أشر إلى بنود ISO 9001:2015 أو ISO 20000-1:2018 بأرقامها)

## 4. توصيات التحسين
- المكاسب السريعة (جهد منخفض × أثر مرتفع)
- التحسينات الاستراتيجية (جهد متوسط-مرتفع × أثر مرتفع)
- الترتيب حسب الأولوية — قم بترقيمها من 1 إلى N

## 5. التوافق مع أفضل الممارسات
- التوافق مع إطار APQC (اذكر المستوى/رمز العملية)
- المقارنات المعيارية مع جهات إسكان حكومية مماثلة
- تقييم مستوى النضج (أولي / محدد / مُدار / مُحسَّن)

## 6. خطة العمل
- الإجراءات الفورية (هذا الأسبوع — المسؤول + تاريخ الاستحقاق)
- الإجراءات قصيرة المدى (هذا الشهر — المسؤول + تاريخ الاستحقاق)
- المبادرات طويلة المدى (هذا الربع — المسؤول + تاريخ الاستحقاق)

اختم بقائمة **الأيام الثلاثون القادمة** تحتوي على 3-5 بنود ملموسة.";

    public const string ProcessGroupAnalysisSectionsEn = @"Produce a DEPARTMENT-LEVEL analysis of the process group below in these six sections, using markdown:

## 1. DEPARTMENT HEALTH ASSESSMENT
- Overall health score (1-10)
- Key strengths across all processes in the group
- Critical weaknesses and cross-process gaps

## 2. EFFICIENCY ANALYSIS
- Cross-process bottlenecks (handoffs, duplicate work, rework loops)
- Department-wide automation opportunities (name the shared systems)
- Resource optimization — where to pool or split capacity

## 3. RISK & COMPLIANCE
- Department-level risk exposure (aggregate the top 5 residual scores)
- Compliance gaps across the group (cite ISO clauses)
- Recommended shared controls (one policy/control fixing multiple processes)

## 4. PERFORMANCE INSIGHTS
- Relative performance of processes in the group (best vs worst)
- Incident and problem trends (cite numbers from the context)
- Service delivery effectiveness (cycle time, throughput, first-time-right)

## 5. STRATEGIC RECOMMENDATIONS
- Top 5 priority improvement areas for the whole group
- Quick wins (0–3 months)
- Long-term strategic initiatives (6–12 months)
- Resource allocation recommendations

## 6. BEST PRACTICES
- Public-sector housing benchmarks
- Leading practices to adopt
- Innovation opportunities (AI, automation, self-service)

Close with a **NEXT 30 DAYS** list of 3–5 concrete items for the department head.";

    public const string ProcessGroupAnalysisSectionsAr = @"أنتج تحليلاً على مستوى الإدارة لمجموعة العمليات أدناه في الأقسام الستة التالية بصيغة ماركداون:

## 1. تقييم صحة الإدارة
- درجة الصحة الإجمالية (1-10)
- نقاط القوة الرئيسية عبر جميع عمليات المجموعة
- نقاط الضعف الحرجة والفجوات بين العمليات

## 2. تحليل الكفاءة
- الاختناقات بين العمليات (التسليمات، العمل المكرر، حلقات إعادة العمل)
- فرص الأتمتة على مستوى الإدارة (اذكر الأنظمة المشتركة)
- تحسين الموارد — أين يمكن تجميع أو تقسيم الطاقة الاستيعابية

## 3. المخاطر والامتثال
- تعرض الإدارة للمخاطر (جمع أعلى 5 درجات مخاطر متبقية)
- فجوات الامتثال عبر المجموعة (أشر إلى بنود ISO)
- الضوابط المشتركة الموصى بها (سياسة واحدة تعالج عدة عمليات)

## 4. رؤى الأداء
- الأداء النسبي للعمليات في المجموعة (الأفضل مقابل الأسوأ)
- اتجاهات الحوادث والمشكلات (اذكر الأرقام من السياق)
- فعالية تقديم الخدمة (زمن الدورة، الإنتاجية، الصحيح من المرة الأولى)

## 5. التوصيات الاستراتيجية
- أهم 5 مجالات تحسين ذات أولوية للمجموعة بأكملها
- المكاسب السريعة (0-3 أشهر)
- المبادرات الاستراتيجية طويلة المدى (6-12 شهر)
- توصيات تخصيص الموارد

## 6. أفضل الممارسات
- المقارنات المعيارية لجهات الإسكان الحكومية
- الممارسات الرائدة التي يمكن تبنيها
- فرص الابتكار (الذكاء الاصطناعي، الأتمتة، الخدمة الذاتية)

اختم بقائمة **الأيام الثلاثون القادمة** تحتوي على 3-5 بنود ملموسة لرئيس الإدارة.";

    // ═══════════════════════════════════════════════════════════════
    // FOCUS INSTRUCTIONS — bias the analysis toward a lens
    // ═══════════════════════════════════════════════════════════════

    public static string GetFocusInstructionEn(string? focus) => focus?.ToLowerInvariant() switch
    {
        "efficiency" => "\n\n**FOCUS: EFFICIENCY.** Lead and expand the Efficiency Analysis section. Specifically surface cycle-time bottlenecks, waste (Lean 8-wastes framework), and automation ROI. Suppress compliance and best-practice sections to 2 bullets each.",
        "risk"       => "\n\n**FOCUS: RISK.** Lead and expand the Risk Assessment section. Map every finding back to the residual-score matrix. Cite ISO 9001:2015 and ISO 27001 clauses. Suppress best-practice benchmarks to 2 bullets.",
        "automation" => "\n\n**FOCUS: AUTOMATION.** Lead and expand the Efficiency Analysis section, but narrow it to automation-only. For each candidate name the system (RPA / AI / integration), estimated ROI, and effort. Suppress health-assessment section to 2 bullets.",
        "customer"   => "\n\n**FOCUS: CUSTOMER EXPERIENCE.** Re-frame every section from the citizen's point of view. For each finding, ask: 'how does this affect the person waiting for their housing decision?' Highlight touchpoint friction, wait times, and communication gaps.",
        _            => string.Empty
    };

    public static string GetFocusInstructionAr(string? focus) => focus?.ToLowerInvariant() switch
    {
        "efficiency" => "\n\n**التركيز: الكفاءة.** اجعل قسم تحليل الكفاءة في المقدمة وقم بتوسيعه. أبرز تحديداً اختناقات زمن الدورة، والهدر (إطار الأنواع الثمانية من الهدر)، وعائد الاستثمار للأتمتة. قلّص أقسام الامتثال وأفضل الممارسات إلى نقطتين لكل منها.",
        "risk"       => "\n\n**التركيز: المخاطر.** اجعل قسم تقييم المخاطر في المقدمة وقم بتوسيعه. اربط كل ملاحظة بمصفوفة الدرجة المتبقية. أشر إلى بنود ISO 9001:2015 و ISO 27001. قلّص مقارنات أفضل الممارسات إلى نقطتين.",
        "automation" => "\n\n**التركيز: الأتمتة.** اجعل قسم تحليل الكفاءة في المقدمة وقم بتوسيعه، لكن اقصره على الأتمتة فقط. لكل مرشح اذكر النظام (RPA / ذكاء اصطناعي / تكامل)، العائد المقدر، والجهد. قلّص قسم تقييم الصحة إلى نقطتين.",
        "customer"   => "\n\n**التركيز: تجربة المتعامل.** أعد صياغة كل قسم من وجهة نظر المواطن. لكل ملاحظة اسأل: 'كيف يؤثر هذا على الشخص الذي ينتظر قرار الإسكان؟' سلط الضوء على احتكاك نقاط التواصل، أوقات الانتظار، وفجوات التواصل.",
        _            => string.Empty
    };

    // ═══════════════════════════════════════════════════════════════
    // ASSEMBLY — build the final string the controller sends to the AI
    // ═══════════════════════════════════════════════════════════════

    public static string BuildProcessAnalysisPrompt(string contextMarkdown, string? focus, bool isArabic)
    {
        var system = isArabic ? SystemPromptAr : SystemPromptEn;
        var glossary = MbrheGlossary.ForCulture(isArabic);
        var fewShot = isArabic ? FewShotExampleAr : FewShotExampleEn;
        var sections = isArabic ? ProcessAnalysisSectionsAr : ProcessAnalysisSectionsEn;
        var focusBias = isArabic ? GetFocusInstructionAr(focus) : GetFocusInstructionEn(focus);

        var contextHeader = isArabic
            ? "\n\n## سياق العملية (المدخلات)\n"
            : "\n\n## PROCESS CONTEXT (INPUT)\n";

        return system + "\n\n" + glossary + "\n\n" + fewShot + sections + contextHeader + contextMarkdown + focusBias;
    }

    public static string BuildProcessGroupAnalysisPrompt(string contextMarkdown, string? focus, bool isArabic)
    {
        var system = isArabic ? SystemPromptAr : SystemPromptEn;
        var glossary = MbrheGlossary.ForCulture(isArabic);
        var fewShot = isArabic ? FewShotExampleAr : FewShotExampleEn;
        var sections = isArabic ? ProcessGroupAnalysisSectionsAr : ProcessGroupAnalysisSectionsEn;
        var focusBias = isArabic ? GetFocusInstructionAr(focus) : GetFocusInstructionEn(focus);

        var contextHeader = isArabic
            ? "\n\n## سياق مجموعة العمليات (المدخلات)\n"
            : "\n\n## PROCESS GROUP CONTEXT (INPUT)\n";

        return system + "\n\n" + glossary + "\n\n" + fewShot + sections + contextHeader + contextMarkdown + focusBias;
    }
}
