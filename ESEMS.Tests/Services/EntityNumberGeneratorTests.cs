using ESEMS.Web.Models.ServiceManagement;
using ESEMS.Web.Services.Common;
using ESEMS.Tests.TestFixtures;

namespace ESEMS.Tests.Services;

public class EntityNumberGeneratorTests
{
    [Fact]
    public async Task GenerateNextNumber_EmptyTable_ReturnsFirst()
    {
        using var context = TestDbContextFactory.Create();
        var generator = new EntityNumberGenerator(context);

        var result = await generator.GenerateNextNumberAsync("INC");

        var year = DateTime.UtcNow.Year;
        Assert.Equal($"INC-{year}-0001", result);
    }

    [Fact]
    public async Task GenerateNextNumber_ExistingRecords_Increments()
    {
        using var context = TestDbContextFactory.Create();
        var year = DateTime.UtcNow.Year;

        context.Incidents.Add(new Incident
        {
            Id = Guid.NewGuid().ToString(),
            IncidentNumber = $"INC-{year}-0003",
            NameEn = "Test Incident",
            ReportedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var generator = new EntityNumberGenerator(context);
        var result = await generator.GenerateNextNumberAsync("INC");

        Assert.Equal($"INC-{year}-0004", result);
    }

    [Fact]
    public async Task GenerateNextNumber_Problem_UsesCorrectPrefix()
    {
        using var context = TestDbContextFactory.Create();
        var year = DateTime.UtcNow.Year;

        context.Problems.Add(new Problem
        {
            Id = Guid.NewGuid().ToString(),
            ProblemNumber = $"PRB-{year}-0005",
            NameEn = "Test Problem",
            IdentifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var generator = new EntityNumberGenerator(context);
        var result = await generator.GenerateNextNumberAsync("PRB");

        Assert.Equal($"PRB-{year}-0006", result);
    }

    [Fact]
    public async Task GenerateNextNumber_DifferentYear_RestartsSequence()
    {
        using var context = TestDbContextFactory.Create();

        // Add an incident from last year
        context.Incidents.Add(new Incident
        {
            Id = Guid.NewGuid().ToString(),
            IncidentNumber = "INC-2025-0099",
            NameEn = "Old Incident",
            ReportedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var generator = new EntityNumberGenerator(context);
        var result = await generator.GenerateNextNumberAsync("INC");

        var currentYear = DateTime.UtcNow.Year;
        Assert.Equal($"INC-{currentYear}-0001", result);
    }
}
