namespace ESEMS.Web.Services.AI.Prompts;

/// <summary>
/// Canonical bilingual vocabulary for every AI feature in ESEMS. Keeping
/// terminology in one place means the Analyzer, the BPMN generator, the
/// Process Optimizer, and any future AI feature all speak the same domain
/// language — "تقديم الطلب" is always "تقديم الطلب", never "تسليم الطلب".
///
/// Why this exists:
/// <list type="bullet">
/// <item>Arabic outputs from LLMs drift between standard Arabic, dialectal
/// Arabic, and direct English transliterations. Pinning the terms forces
/// consistency.</item>
/// <item>Analysts reading both EN and AR reports side-by-side expect the
/// same concepts to map 1:1 between languages.</item>
/// <item>ISO / APQC terminology has accepted Arabic translations — the
/// model should use those, not invent new ones.</item>
/// </list>
/// </summary>
public static class MbrheGlossary
{
    public const string GlossaryEn = @"## MBRHE DOMAIN GLOSSARY — use these terms consistently
- **Process owner** → the person accountable for a process end-to-end
- **Activity** → a single executable step inside a process (APQC L4)
- **Handoff** → the transition of work between two activities in different lanes
- **Cycle time** → total elapsed time from process start to process end
- **First-time-right** → percentage of cases completed without rework
- **SLA breach** → an instance where a service level agreement was missed
- **Citizen / Applicant / Beneficiary** → the UAE national receiving the service
- **UAE Pass** → the federal digital identity service (preferred over ""login"")
- **Emirates ID** → the national ID card (preferred over ""national ID"")
- **Housing loan** → a financing product offered by MBRHE (purchase/construction/replacement/maintenance)
- **Housing grant** → a non-repayable benefit (land/house/apartment/construction/maintenance)
- **Handover** → physical delivery of a housing unit to the beneficiary
- **Residual risk** → risk remaining after existing controls are applied
- **Inherent risk** → risk before any controls (impact × likelihood, 1–25)
- **Control** → a preventive or detective measure that reduces risk
- **Root cause** → the underlying reason an incident or problem occurred
- **Corrective action** → action to remove the root cause of a nonconformity
- **Preventive action** → action to prevent a potential nonconformity

## PROCESS TERMINOLOGY
- Use ""Review"" (not ""check"") for human verification steps
- Use ""Approve"" / ""Reject"" (not ""accept""/""deny"") for gateway decisions
- Use ""Submit"" (not ""send"") for citizen-facing transactions
- Use ""Receive"" (not ""get"") for inbound events";

    public const string GlossaryAr = @"## مصطلحات مؤسسة محمد بن راشد للإسكان — استخدمها بثبات
- **مالك العملية** → الشخص المسؤول مسؤولية كاملة عن العملية من البداية إلى النهاية
- **النشاط** → خطوة تنفيذية واحدة داخل العملية (المستوى الرابع في APQC)
- **التسليم (Handoff)** → انتقال العمل بين نشاطين في حارتين مختلفتين
- **زمن الدورة** → الوقت الإجمالي المنقضي من بداية العملية إلى نهايتها
- **الصحيح من المرة الأولى** → نسبة الحالات المكتملة دون إعادة عمل
- **خرق اتفاقية مستوى الخدمة** → حالة تم فيها تجاوز اتفاقية مستوى الخدمة
- **المواطن / المتعامل / المستفيد** → المواطن الإماراتي الذي يتلقى الخدمة (تفضيل كلمة ""المتعامل"" في واجهات الخدمة، و""المواطن"" في السياق الاستراتيجي)
- **الهوية الرقمية UAE Pass** → الخدمة الاتحادية للهوية الرقمية (أفضل من كلمة ""تسجيل الدخول"")
- **الهوية الإماراتية** → بطاقة الهوية الوطنية (أفضل من ""الهوية الوطنية"")
- **القرض السكني** → منتج تمويلي تقدمه المؤسسة (شراء/بناء/استبدال/صيانة)
- **المنحة السكنية** → منفعة غير مستردة (أرض/منزل/شقة/بناء/صيانة)
- **التسليم النهائي** → التسليم الفعلي للوحدة السكنية للمستفيد
- **المخاطرة المتبقية** → المخاطرة الباقية بعد تطبيق الضوابط الحالية
- **المخاطرة الكامنة** → المخاطرة قبل أي ضوابط (الأثر × الاحتمالية، 1-25)
- **الضابط / الضبط الرقابي** → إجراء وقائي أو استكشافي يقلل من المخاطر
- **السبب الجذري** → السبب الأساسي لحادثة أو مشكلة
- **الإجراء التصحيحي** → إجراء لإزالة السبب الجذري لعدم المطابقة
- **الإجراء الوقائي** → إجراء لمنع عدم مطابقة محتملة

## مصطلحات العمليات
- استخدم ""مراجعة"" (ليس ""فحص"") لخطوات التحقق البشري
- استخدم ""اعتماد"" / ""رفض"" (ليس ""موافقة""/""عدم موافقة"") لقرارات البوابات
- استخدم ""تقديم"" (ليس ""إرسال"") للمعاملات التي يقدمها المتعامل
- استخدم ""استلام"" (ليس ""أخذ"") للأحداث الواردة

## قواعد لغوية مهمة
- اكتب الأسماء العربية للمعايير الدولية بصيغتها الإنجليزية الأصلية بين قوسين: ""نظام إدارة الجودة (ISO 9001:2015)""
- استخدم الأرقام العربية الشرقية فقط إذا كان المستند كاملاً بالأرقام العربية؛ وإلا فضّل الأرقام العربية الغربية (0-9)
- لا تخلط الإنجليزية والعربية في نفس الجملة إلا للعلامات التجارية أو المصطلحات التقنية التي ليس لها مقابل عربي شائع
- حافظ على الفصحى — لا لهجة محلية";

    /// <summary>
    /// Returns the glossary for the current UI culture. Empty string if
    /// no glossary is appropriate (e.g. a third language).
    /// </summary>
    public static string ForCulture(bool isArabic) => isArabic ? GlossaryAr : GlossaryEn;
}
