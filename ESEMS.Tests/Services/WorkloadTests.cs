using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.WorkloadAnalysis;
using ESEMS.Web.Services.Workload;

namespace ESEMS.Tests.Services;

public class WorkloadScenarioStatusMachineTests
{
    [Theory]
    [InlineData(WorkloadScenarioStatus.Draft,    WorkloadScenarioStatus.InReview,   true)]
    [InlineData(WorkloadScenarioStatus.Draft,    WorkloadScenarioStatus.Archived,   true)]
    [InlineData(WorkloadScenarioStatus.Draft,    WorkloadScenarioStatus.Approved,   false)]
    [InlineData(WorkloadScenarioStatus.InReview, WorkloadScenarioStatus.Approved,   true)]
    [InlineData(WorkloadScenarioStatus.InReview, WorkloadScenarioStatus.Draft,      true)]
    [InlineData(WorkloadScenarioStatus.Approved, WorkloadScenarioStatus.InReview,   true)]
    [InlineData(WorkloadScenarioStatus.Approved, WorkloadScenarioStatus.Draft,      false)]
    [InlineData(WorkloadScenarioStatus.Archived, WorkloadScenarioStatus.Draft,      false)]
    [InlineData(WorkloadScenarioStatus.Archived, WorkloadScenarioStatus.InReview,   false)]
    public void CanTransition_MatchesStateMachine(WorkloadScenarioStatus from, WorkloadScenarioStatus to, bool expected)
    {
        Assert.Equal(expected, WorkloadScenarioStatusMachine.CanTransition(from, to));
    }

    [Fact]
    public void CanTransition_AllowsNoOp()
    {
        foreach (WorkloadScenarioStatus s in Enum.GetValues(typeof(WorkloadScenarioStatus)))
            Assert.True(WorkloadScenarioStatusMachine.CanTransition(s, s));
    }

    [Fact]
    public void IsEditable_TrueExceptForArchived()
    {
        Assert.True(WorkloadScenarioStatusMachine.IsEditable(WorkloadScenarioStatus.Draft));
        Assert.True(WorkloadScenarioStatusMachine.IsEditable(WorkloadScenarioStatus.InReview));
        Assert.True(WorkloadScenarioStatusMachine.IsEditable(WorkloadScenarioStatus.Approved));
        Assert.False(WorkloadScenarioStatusMachine.IsEditable(WorkloadScenarioStatus.Archived));
    }

    [Fact]
    public void AllowedNext_TerminalReturnsEmpty()
    {
        Assert.Empty(WorkloadScenarioStatusMachine.AllowedNext(WorkloadScenarioStatus.Archived));
    }
}

public class WorkloadConfigMathTests
{
    [Fact]
    public void GrossWorkingDaysPerYear_SubtractsPublicHolidays()
    {
        var c = new WorkloadConfig { WorkingDaysPerWeek = 5, PublicHolidaysPerYear = 12 };
        // 5 * 52 = 260 working days gross, minus 12 holidays
        Assert.Equal(248, c.GrossWorkingDaysPerYear);
    }

    [Fact]
    public void AbsenceDays_SumsLeaveSickTraining()
    {
        var c = new WorkloadConfig { AnnualLeaveDays = 22, AverageSickDays = 7, TrainingDaysPerYear = 7 };
        Assert.Equal(36, c.AbsenceDays);
    }

    [Fact]
    public void NetAvailableHoursPerFTE_AppliesOverheadAndUtilization()
    {
        var c = new WorkloadConfig
        {
            WorkingHoursPerDay = 7.5m,
            WorkingDaysPerWeek = 5,
            PublicHolidaysPerYear = 12,
            AnnualLeaveDays = 22,
            AverageSickDays = 7,
            TrainingDaysPerYear = 7,
            AdminOverheadPercent = 15m,
            TargetUtilizationRate = 0.80m,
        };
        // Net days = 260 - 12 - 36 = 212; × 7.5h = 1590; × (1 - 0.15) = 1351.5; × 0.80 = 1081.2
        Assert.Equal(1081.2m, c.NetAvailableHoursPerFTE);
    }

    [Fact]
    public void AllowanceFactor_MatchesNetOverGross()
    {
        var c = new WorkloadConfig
        {
            WorkingHoursPerDay = 8m,
            WorkingDaysPerWeek = 5,
            PublicHolidaysPerYear = 10,
            AnnualLeaveDays = 20,
            AverageSickDays = 5,
            TrainingDaysPerYear = 5,
            AdminOverheadPercent = 10m,
            TargetUtilizationRate = 0.9m,
        };
        var expected = c.NetAvailableHoursPerFTE / c.GrossAnnualHours;
        Assert.Equal(expected, c.AllowanceFactor);
    }
}

public class WorkloadLineItemMathTests
{
    [Fact]
    public void WeightedVolume_ReturnsRawAnnual_WhenComplexityDisabled()
    {
        var item = new WorkloadLineItem
        {
            AnnualVolume = 1000,
            ComplexityEnabled = false,
            SimpleVolumePercent = 70m,  // ignored
            MediumMult = 10m,           // ignored
        };
        Assert.Equal(1000m, item.WeightedVolume);
    }

    [Fact]
    public void WeightedVolume_AppliesComplexityBandsAndMultipliers()
    {
        var item = new WorkloadLineItem
        {
            AnnualVolume = 1000,
            ComplexityEnabled = true,
            SimpleVolumePercent  = 50m, SimpleMult  = 1.0m,
            MediumVolumePercent  = 30m, MediumMult  = 1.5m,
            ComplexVolumePercent = 20m, ComplexMult = 2.5m,
        };
        // 500*1 + 300*1.5 + 200*2.5 = 500 + 450 + 500 = 1450
        Assert.Equal(1450m, item.WeightedVolume);
    }

    [Fact]
    public void WorkloadHours_ConvertsMinutesToHours()
    {
        var item = new WorkloadLineItem { AnnualVolume = 100, AvgProcessingTimeMinutes = 30m };
        // 100 * 30 / 60 = 50 hours
        Assert.Equal(50m, item.WorkloadHours);
    }

    [Fact]
    public void RequiredFTE_ZeroWhenNoScenarioConfig()
    {
        var item = new WorkloadLineItem { AnnualVolume = 100, AvgProcessingTimeMinutes = 30m, Scenario = null };
        Assert.Equal(0m, item.RequiredFTE);
    }

    [Fact]
    public void RequiredFTE_DividesHoursByNetFteCapacity()
    {
        var config = new WorkloadConfig
        {
            WorkingHoursPerDay = 7.5m, WorkingDaysPerWeek = 5,
            PublicHolidaysPerYear = 12, AnnualLeaveDays = 22, AverageSickDays = 7, TrainingDaysPerYear = 7,
            AdminOverheadPercent = 15m, TargetUtilizationRate = 0.80m,
        };
        var scenario = new WorkloadScenario { Config = config };
        var item = new WorkloadLineItem
        {
            AnnualVolume = 5000,
            AvgProcessingTimeMinutes = 30m,
            Scenario = scenario,
        };
        // WorkloadHours = 5000 * 30 / 60 = 2500
        // NetAvailableHoursPerFTE = 1081.2
        // RequiredFTE = 2500 / 1081.2 ≈ 2.312
        Assert.InRange(item.RequiredFTE, 2.31m, 2.32m);
    }
}
