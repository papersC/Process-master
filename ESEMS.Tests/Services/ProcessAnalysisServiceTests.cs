using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Services.Analysis;
using ESEMS.Tests.TestFixtures;

namespace ESEMS.Tests.Services;

public class ProcessAnalysisServiceTests
{
    [Fact]
    public async Task BuildProcessGroupContext_ReturnsEmptyForMissingGroup()
    {
        using var context = TestDbContextFactory.Create();
        var service = new ProcessAnalysisService(context);

        var result = await service.BuildProcessGroupAnalysisContextAsync("nonexistent");

        Assert.Empty(result.ContextMarkdown);
    }

    [Fact]
    public async Task BuildProcessGroupContext_AggregatesMetrics()
    {
        using var context = TestDbContextFactory.Create();

        var category = new Category
        {
            Id = Guid.NewGuid().ToString(),
            NameEn = "Test Category",
            NameAr = "فئة اختبار",
            Code = "CAT-01",
            CreatedAt = DateTime.UtcNow
        };
        context.Categories.Add(category);

        var pg = new ProcessGroup
        {
            Id = Guid.NewGuid().ToString(),
            NameEn = "Test Group",
            NameAr = "مجموعة اختبار",
            Code = "PG-01",
            CategoryId = category.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.ProcessGroups.Add(pg);

        var process = new Process
        {
            Id = Guid.NewGuid().ToString(),
            NameEn = "Test Process",
            NameAr = "عملية اختبار",
            Code = "P-01",
            ProcessGroupId = pg.Id,
            Status = ProcessStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        context.Processes.Add(process);

        var activity = new Activity
        {
            Id = Guid.NewGuid().ToString(),
            NameEn = "Test Activity",
            NameAr = "نشاط اختبار",
            Code = "A-01",
            ProcessId = process.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Activities.Add(activity);

        await context.SaveChangesAsync();

        var service = new ProcessAnalysisService(context);
        var result = await service.BuildProcessGroupAnalysisContextAsync(pg.Id);

        Assert.Contains("Test Group", result.ContextMarkdown);
        Assert.Contains("PG-01", result.ContextMarkdown);
        Assert.Equal(1, result.ActivityCount);
    }

    [Fact]
    public async Task BuildProcessContext_IncludesRelatedEntities()
    {
        using var context = TestDbContextFactory.Create();

        var category = new Category
        {
            Id = Guid.NewGuid().ToString(),
            NameEn = "Cat",
            NameAr = "فئة",
            Code = "C1",
            CreatedAt = DateTime.UtcNow
        };
        context.Categories.Add(category);

        var pg = new ProcessGroup
        {
            Id = Guid.NewGuid().ToString(),
            NameEn = "Group",
            NameAr = "مجموعة",
            Code = "G1",
            CategoryId = category.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.ProcessGroups.Add(pg);

        var process = new Process
        {
            Id = Guid.NewGuid().ToString(),
            NameEn = "Main Process",
            NameAr = "العملية الرئيسية",
            Code = "MP-01",
            ProcessGroupId = pg.Id,
            Status = ProcessStatus.Active,
            BpmnDiagram = "<bpmn/>",
            CreatedAt = DateTime.UtcNow
        };
        context.Processes.Add(process);
        await context.SaveChangesAsync();

        var service = new ProcessAnalysisService(context);
        var result = await service.BuildProcessAnalysisContextAsync(process.Id);

        Assert.Contains("Main Process", result.ContextMarkdown);
        Assert.Contains("MP-01", result.ContextMarkdown);
        Assert.True(result.HasBpmn);
    }

    [Fact]
    public void BuildOptimizedBpmnPrompt_ReturnsNonEmptyPrompt()
    {
        using var context = TestDbContextFactory.Create();
        var service = new ProcessAnalysisService(context);

        var prompt = service.BuildOptimizedBpmnPrompt();

        Assert.NotEmpty(prompt);
        Assert.Contains("BPMN", prompt);
        Assert.Contains("START EVENT", prompt);
        Assert.Contains("END EVENT", prompt);
    }

    [Fact]
    public void CleanOptimizedPromptResponse_RemovesWrapperPhrases()
    {
        using var context = TestDbContextFactory.Create();
        var service = new ProcessAnalysisService(context);

        var response = "Here is the optimized prompt: The housing application process starts when...";
        var result = service.CleanOptimizedPromptResponse(response);

        Assert.Equal("The housing application process starts when...", result);
    }

    [Fact]
    public void CleanOptimizedPromptResponse_TrimsWhitespace()
    {
        using var context = TestDbContextFactory.Create();
        var service = new ProcessAnalysisService(context);

        var result = service.CleanOptimizedPromptResponse("  Some text  ");
        Assert.Equal("Some text", result);
    }
}
