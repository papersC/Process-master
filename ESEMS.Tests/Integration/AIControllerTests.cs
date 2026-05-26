using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ESEMS.Tests.Integration;

public class AIControllerTests : IClassFixture<EsemsTestFactoryWithAi>
{
    private readonly EsemsTestFactoryWithAi _factory;
    public AIControllerTests(EsemsTestFactoryWithAi factory) { _factory = factory; }

    [Fact]
    public async Task Diagrams_Page_RendersForAuthedUser()
    {
        var client = _factory.CreateClient();
        var r = await client.GetAsync("/AI/Diagrams");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Diagrams_Page_RequiresAuth()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Role", "ANON");

        var r = await client.GetAsync("/AI/Diagrams");

        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task ProcessAnalyzer_Page_RendersForAuthedUser()
    {
        var client = _factory.CreateClient();
        var r = await client.GetAsync("/AI/ProcessAnalyzer");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task GetProcesses_ReturnsJson_WithSuccessFlag()
    {
        var client = _factory.CreateClient();
        var r = await client.GetAsync("/AI/GetProcesses");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("application/json", r.Content.Headers.ContentType?.MediaType);
        var body = await r.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":true", body);
    }

    [Fact]
    public async Task GetProcessesWithBPMN_ReturnsJson_EvenWhenEmpty()
    {
        var client = _factory.CreateClient();
        var r = await client.GetAsync("/AI/GetProcessesWithBPMN");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.Content.ReadAsStringAsync();
        Assert.Contains("\"processes\"", body);
    }
}
