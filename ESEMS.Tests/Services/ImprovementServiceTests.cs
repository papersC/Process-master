using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.Improvement;
using ESEMS.Web.Services.Improvements;
using ESEMS.Tests.TestFixtures;

namespace ESEMS.Tests.Services;

public class ImprovementServiceTests
{
    [Fact]
    public void GenerateRoadmap_AssignsCorrectPhases()
    {
        using var context = TestDbContextFactory.Create();
        var service = new ImprovementWorkflowService(context);

        var improvements = new List<ImprovementInitiative>
        {
            CreateImprovement("QW1", ImprovementQuadrant.QuickWins, ImprovementHorizon.Horizon1_Current),
            CreateImprovement("MP1", ImprovementQuadrant.MajorProjects, ImprovementHorizon.Horizon1_Current),
            CreateImprovement("FI1", ImprovementQuadrant.FillIns, ImprovementHorizon.Horizon1_Current),
            CreateImprovement("TT1", ImprovementQuadrant.ThanklessTasks, ImprovementHorizon.Horizon1_Current),
        };

        var roadmap = service.GenerateRoadmap(improvements);

        Assert.Equal(3, roadmap.Phases.Count);
        Assert.Equal(4, roadmap.Phases[0].Initiatives.Count); // Phase 1 has all of them
        Assert.Empty(roadmap.Phases[1].Initiatives); // Phase 2 empty
        Assert.Empty(roadmap.Phases[2].Initiatives); // Phase 3 empty

        // Check order within Phase 1
        Assert.Equal("QW1", roadmap.Phases[0].Initiatives[0].TitleEn);
        Assert.Equal("MP1", roadmap.Phases[0].Initiatives[1].TitleEn);
        Assert.Equal("FI1", roadmap.Phases[0].Initiatives[2].TitleEn);
        Assert.Equal("TT1", roadmap.Phases[0].Initiatives[3].TitleEn);
    }

    [Fact]
    public void GenerateRoadmap_HorizonShiftsPhase()
    {
        using var context = TestDbContextFactory.Create();
        var service = new ImprovementWorkflowService(context);

        // QW + H3 should shift from phase 1 to phase 3
        var improvements = new List<ImprovementInitiative>
        {
            CreateImprovement("QW-H3", ImprovementQuadrant.QuickWins, ImprovementHorizon.Horizon3_Future),
        };

        var roadmap = service.GenerateRoadmap(improvements);

        Assert.Empty(roadmap.Phases[0].Initiatives); // Phase 1 empty
        Assert.Empty(roadmap.Phases[1].Initiatives); // Phase 2 empty
        Assert.Single(roadmap.Phases[2].Initiatives); // Phase 3 has it
    }

    [Fact]
    public void GenerateRoadmap_SortsByPriorityScore()
    {
        using var context = TestDbContextFactory.Create();
        var service = new ImprovementWorkflowService(context);

        var low = CreateImprovement("Low", ImprovementQuadrant.QuickWins, ImprovementHorizon.Horizon1_Current);
        low.ImpactScore = 2;

        var high = CreateImprovement("High", ImprovementQuadrant.QuickWins, ImprovementHorizon.Horizon1_Current);
        high.ImpactScore = 9;

        var roadmap = service.GenerateRoadmap(new List<ImprovementInitiative> { low, high });

        var phase1 = roadmap.Phases[0].Initiatives;
        Assert.Equal(2, phase1.Count);
        // Both have null TotalPrioritizationScore, so sorts by ImpactScore desc
        Assert.Equal("High", phase1[0].TitleEn);
    }

    [Fact]
    public void GenerateRoadmap_CalculatesCumulativeSavings()
    {
        using var context = TestDbContextFactory.Create();
        var service = new ImprovementWorkflowService(context);

        var qw = CreateImprovement("QW", ImprovementQuadrant.QuickWins, ImprovementHorizon.Horizon1_Current);
        qw.EstimatedCostSavings = 1000;

        var mp = CreateImprovement("MP", ImprovementQuadrant.MajorProjects, ImprovementHorizon.Horizon2_Expand);
        mp.EstimatedCostSavings = 2000;

        var roadmap = service.GenerateRoadmap(new List<ImprovementInitiative> { qw, mp });

        Assert.Equal(3, roadmap.CumulativeSavings.Count);
        Assert.Equal(1000m, roadmap.CumulativeSavings[0]); // Phase 1: 1000
        Assert.Equal(3000m, roadmap.CumulativeSavings[1]); // Phase 2: 1000 + 2000
        Assert.Equal(3000m, roadmap.CumulativeSavings[2]); // Phase 3: 3000 + 0
    }

    [Fact]
    public void ValidateWizardInput_MissingTitle_ReturnsErrors()
    {
        using var context = TestDbContextFactory.Create();
        var service = new ImprovementWorkflowService(context);

        var improvement = new ImprovementInitiative { ProcessId = "P1" };
        var mp = new WizardMeasurementParams { MeasurementTypes = "Satisfaction" };

        var result = service.ValidateWizardInput(improvement, new[] { "P1" }, null, mp);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Title (English)"));
        Assert.Contains(result.Errors, e => e.Contains("Title (Arabic)"));
    }

    [Fact]
    public void ValidateWizardInput_NoProcessOrService_ReturnsError()
    {
        using var context = TestDbContextFactory.Create();
        var service = new ImprovementWorkflowService(context);

        var improvement = new ImprovementInitiative
        {
            TitleEn = "Test",
            TitleAr = "اختبار"
        };
        var mp = new WizardMeasurementParams { MeasurementTypes = "Satisfaction" };

        var result = service.ValidateWizardInput(improvement, null, null, mp);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Process or Service"));
    }

    [Fact]
    public void ValidateWizardInput_ValidInput_Passes()
    {
        using var context = TestDbContextFactory.Create();
        var service = new ImprovementWorkflowService(context);

        var improvement = new ImprovementInitiative
        {
            TitleEn = "Test Improvement",
            TitleAr = "تحسين اختباري"
        };
        var mp = new WizardMeasurementParams
        {
            MeasurementTypes = "Satisfaction",
            MeasurementNames = "Customer Score",
            MeasurementNamesAr = "درجة العميل",
            MeasurementUnits = "Score",
            MeasurementTargets = "95",
            MeasurementAsIs = "80"
        };

        var result = service.ValidateWizardInput(improvement, new[] { "P1" }, null, mp);

        Assert.True(result.IsValid);
        Assert.Single(result.NormalizedProcessIds);
    }

    [Fact]
    public void ValidateWizardInput_DeduplicatesIds()
    {
        using var context = TestDbContextFactory.Create();
        var service = new ImprovementWorkflowService(context);

        var improvement = new ImprovementInitiative
        {
            TitleEn = "Test",
            TitleAr = "اختبار",
            ProcessId = "P1" // Also in the array
        };
        var mp = new WizardMeasurementParams
        {
            MeasurementTypes = "Cost",
            MeasurementNames = "Savings",
            MeasurementUnits = "AED",
            MeasurementTargets = "1000",
            MeasurementAsIs = "500"
        };

        var result = service.ValidateWizardInput(improvement, new[] { "P1", "P1", "P2" }, null, mp);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.NormalizedProcessIds.Count); // P1 and P2, deduplicated
    }

    [Fact]
    public async Task CalculateDashboardMetrics_ReturnsCorrectCounts()
    {
        using var context = TestDbContextFactory.Create();

        context.ImprovementInitiatives.Add(CreateImprovement("QW", ImprovementQuadrant.QuickWins, ImprovementHorizon.Horizon1_Current));
        context.ImprovementInitiatives.Add(CreateImprovement("MP", ImprovementQuadrant.MajorProjects, ImprovementHorizon.Horizon2_Expand));
        context.ImprovementInitiatives.Add(CreateImprovement("FI", ImprovementQuadrant.FillIns, ImprovementHorizon.Horizon3_Future));
        await context.SaveChangesAsync();

        var service = new ImprovementWorkflowService(context);
        var metrics = await service.CalculateDashboardMetricsAsync();

        Assert.Equal(3, metrics.TotalInitiatives);
        Assert.Equal(1, metrics.QuickWinsCount);
        Assert.Equal(1, metrics.MajorProjectsCount);
        Assert.Equal(1, metrics.FillInsCount);
        Assert.Equal(0, metrics.ThanklessTasksCount);
        Assert.Equal(1, metrics.H1Count);
        Assert.Equal(1, metrics.H2Count);
        Assert.Equal(1, metrics.H3Count);
    }

    private static ImprovementInitiative CreateImprovement(
        string title,
        ImprovementQuadrant quadrant,
        ImprovementHorizon horizon)
    {
        return new ImprovementInitiative
        {
            Id = Guid.NewGuid().ToString(),
            TitleEn = title,
            TitleAr = title,
            Quadrant = quadrant,
            Horizon = horizon,
            Status = ImprovementStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
