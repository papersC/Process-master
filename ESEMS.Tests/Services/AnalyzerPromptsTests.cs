using ESEMS.Web.Services.AI.Prompts;

namespace ESEMS.Tests.Services;

/// <summary>
/// Verifies the bilingual prompt builders used by the AI Process Analyzer.
/// Tests are structural — they check that a built prompt contains the
/// expected system context, glossary, few-shot example, section headings,
/// focus bias, and context markdown — not the AI output itself.
/// </summary>
public class AnalyzerPromptsTests
{
    private const string SampleContext = "## Process: Test Loan Review\n- Activities: 5\n- Risks: 2";

    // ═══════════════════════════════════════════════════════════════
    // English prompt
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Process_English_ContainsSystemPersona()
    {
        var prompt = AnalyzerPrompts.BuildProcessAnalysisPrompt(SampleContext, null, isArabic: false);
        Assert.Contains("senior business analyst", prompt);
        Assert.Contains("MBRHE", prompt);
        Assert.Contains("APQC Process Classification Framework", prompt);
    }

    [Fact]
    public void Process_English_ContainsDomainGlossary()
    {
        var prompt = AnalyzerPrompts.BuildProcessAnalysisPrompt(SampleContext, null, isArabic: false);
        Assert.Contains("MBRHE DOMAIN GLOSSARY", prompt);
        Assert.Contains("UAE Pass", prompt);
        Assert.Contains("Emirates ID", prompt);
        Assert.Contains("Residual risk", prompt);
        Assert.Contains("Root cause", prompt);
    }

    [Fact]
    public void Process_English_ContainsFewShotExample()
    {
        var prompt = AnalyzerPrompts.BuildProcessAnalysisPrompt(SampleContext, null, isArabic: false);
        Assert.Contains("EXAMPLE (for reference only", prompt);
        Assert.Contains("END OF EXAMPLE", prompt);
    }

    [Fact]
    public void Process_English_ContainsAllSixSections()
    {
        var prompt = AnalyzerPrompts.BuildProcessAnalysisPrompt(SampleContext, null, isArabic: false);
        Assert.Contains("## 1. PROCESS HEALTH ASSESSMENT", prompt);
        Assert.Contains("## 2. EFFICIENCY ANALYSIS", prompt);
        Assert.Contains("## 3. RISK ASSESSMENT", prompt);
        Assert.Contains("## 4. IMPROVEMENT RECOMMENDATIONS", prompt);
        Assert.Contains("## 5. BEST PRACTICE ALIGNMENT", prompt);
        Assert.Contains("## 6. ACTION PLAN", prompt);
    }

    [Fact]
    public void Process_English_ContainsContextMarkdown()
    {
        var prompt = AnalyzerPrompts.BuildProcessAnalysisPrompt(SampleContext, null, isArabic: false);
        Assert.Contains("Test Loan Review", prompt);
        Assert.Contains("PROCESS CONTEXT (INPUT)", prompt);
    }

    [Fact]
    public void Process_English_FocusInstructionInjected()
    {
        var prompt = AnalyzerPrompts.BuildProcessAnalysisPrompt(SampleContext, "risk", isArabic: false);
        Assert.Contains("FOCUS: RISK", prompt);
        Assert.Contains("Risk Assessment section", prompt);
    }

    [Fact]
    public void Process_English_FocusOmittedWhenNull()
    {
        var prompt = AnalyzerPrompts.BuildProcessAnalysisPrompt(SampleContext, null, isArabic: false);
        Assert.DoesNotContain("FOCUS: RISK", prompt);
        Assert.DoesNotContain("FOCUS: EFFICIENCY", prompt);
    }

    // ═══════════════════════════════════════════════════════════════
    // Arabic prompt
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Process_Arabic_ContainsSystemPersonaInArabic()
    {
        var prompt = AnalyzerPrompts.BuildProcessAnalysisPrompt(SampleContext, null, isArabic: true);
        Assert.Contains("محلل أعمال كبير", prompt);
        Assert.Contains("مؤسسة محمد بن راشد للإسكان", prompt);
    }

    [Fact]
    public void Process_Arabic_ContainsDomainGlossaryInArabic()
    {
        var prompt = AnalyzerPrompts.BuildProcessAnalysisPrompt(SampleContext, null, isArabic: true);
        Assert.Contains("مصطلحات مؤسسة محمد بن راشد للإسكان", prompt);
        Assert.Contains("UAE Pass", prompt); // international brand name in Latin
        Assert.Contains("المخاطرة المتبقية", prompt);
        Assert.Contains("السبب الجذري", prompt);
    }

    [Fact]
    public void Process_Arabic_GlossaryEnforcesVerbChoices()
    {
        var prompt = AnalyzerPrompts.BuildProcessAnalysisPrompt(SampleContext, null, isArabic: true);
        // Canonical verb choices from the glossary
        Assert.Contains("\"مراجعة\"", prompt); // review
        Assert.Contains("\"اعتماد\"", prompt); // approve
        Assert.Contains("\"تقديم\"", prompt);  // submit
    }

    [Fact]
    public void Process_Arabic_ContainsAllSixArabicSections()
    {
        var prompt = AnalyzerPrompts.BuildProcessAnalysisPrompt(SampleContext, null, isArabic: true);
        Assert.Contains("## 1. تقييم صحة العملية", prompt);
        Assert.Contains("## 2. تحليل الكفاءة", prompt);
        Assert.Contains("## 3. تقييم المخاطر", prompt);
        Assert.Contains("## 4. توصيات التحسين", prompt);
        Assert.Contains("## 5. التوافق مع أفضل الممارسات", prompt);
        Assert.Contains("## 6. خطة العمل", prompt);
    }

    [Fact]
    public void Process_Arabic_FewShotExampleInArabic()
    {
        var prompt = AnalyzerPrompts.BuildProcessAnalysisPrompt(SampleContext, null, isArabic: true);
        Assert.Contains("مثال مرجعي", prompt);
        Assert.Contains("نهاية المثال", prompt);
    }

    [Fact]
    public void Process_Arabic_FocusInstructionInArabic()
    {
        var prompt = AnalyzerPrompts.BuildProcessAnalysisPrompt(SampleContext, "customer", isArabic: true);
        Assert.Contains("التركيز: تجربة المتعامل", prompt);
    }

    [Fact]
    public void Process_Arabic_ContextHeaderInArabic()
    {
        var prompt = AnalyzerPrompts.BuildProcessAnalysisPrompt(SampleContext, null, isArabic: true);
        Assert.Contains("سياق العملية (المدخلات)", prompt);
    }

    // ═══════════════════════════════════════════════════════════════
    // Process group variant
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ProcessGroup_English_DepartmentLevelSections()
    {
        var prompt = AnalyzerPrompts.BuildProcessGroupAnalysisPrompt(SampleContext, null, isArabic: false);
        Assert.Contains("## 1. DEPARTMENT HEALTH ASSESSMENT", prompt);
        Assert.Contains("## 2. EFFICIENCY ANALYSIS", prompt);
        Assert.Contains("## 3. RISK & COMPLIANCE", prompt);
        Assert.Contains("## 6. BEST PRACTICES", prompt);
    }

    [Fact]
    public void ProcessGroup_Arabic_DepartmentLevelSections()
    {
        var prompt = AnalyzerPrompts.BuildProcessGroupAnalysisPrompt(SampleContext, null, isArabic: true);
        Assert.Contains("## 1. تقييم صحة الإدارة", prompt);
        Assert.Contains("## 3. المخاطر والامتثال", prompt);
        Assert.Contains("## 6. أفضل الممارسات", prompt);
    }

    // ═══════════════════════════════════════════════════════════════
    // Focus instruction branches (all 4 values × both languages)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("efficiency", "FOCUS: EFFICIENCY")]
    [InlineData("risk",       "FOCUS: RISK")]
    [InlineData("automation", "FOCUS: AUTOMATION")]
    [InlineData("customer",   "FOCUS: CUSTOMER EXPERIENCE")]
    public void FocusEnglish_KnownValue_ReturnsNonEmpty(string focus, string expectedMarker)
    {
        var result = AnalyzerPrompts.GetFocusInstructionEn(focus);
        Assert.Contains(expectedMarker, result);
    }

    [Theory]
    [InlineData("efficiency", "التركيز: الكفاءة")]
    [InlineData("risk",       "التركيز: المخاطر")]
    [InlineData("automation", "التركيز: الأتمتة")]
    [InlineData("customer",   "التركيز: تجربة المتعامل")]
    public void FocusArabic_KnownValue_ReturnsNonEmpty(string focus, string expectedMarker)
    {
        var result = AnalyzerPrompts.GetFocusInstructionAr(focus);
        Assert.Contains(expectedMarker, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-focus")]
    public void Focus_UnknownValue_ReturnsEmpty(string? focus)
    {
        Assert.Equal(string.Empty, AnalyzerPrompts.GetFocusInstructionEn(focus));
        Assert.Equal(string.Empty, AnalyzerPrompts.GetFocusInstructionAr(focus));
    }

    // ═══════════════════════════════════════════════════════════════
    // Case-insensitive focus matching
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("EFFICIENCY")]
    [InlineData("Efficiency")]
    [InlineData("eFfIcIeNcY")]
    public void Focus_IsCaseInsensitive(string focus)
    {
        var result = AnalyzerPrompts.GetFocusInstructionEn(focus);
        Assert.Contains("FOCUS: EFFICIENCY", result);
    }
}
