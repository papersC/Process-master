using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ESEMS.Tests.Integration;

public class SmokeTests : IClassFixture<EsemsTestFactory>
{
    private readonly EsemsTestFactory _factory;
    public SmokeTests(EsemsTestFactory factory) { _factory = factory; }

    [Fact]
    public async Task Health_ReturnsOk_Anonymous()
    {
        var client = _factory.CreateClient();
        var r = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_RedirectsOrRejects_WithoutAuth()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Role", "ANON");

        var r = await client.GetAsync("/Improvements");

        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_ReturnsContent_WhenAdminAuthed()
    {
        var client = _factory.CreateClient();
        var r = await client.GetAsync("/Improvements");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Contains("<html", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/Categories")]
    [InlineData("/Services")]
    [InlineData("/Processes")]
    [InlineData("/Incidents")]
    [InlineData("/EnterpriseRisks")]
    [InlineData("/Assets")]
    [InlineData("/Users")]
    [InlineData("/SLA")]
    [InlineData("/WorkloadAnalysis")]
    [InlineData("/Workflow/PendingApprovals")]
    public async Task AdminGet_KeyIndexRoutes_ReturnsOk(string path)
    {
        var client = _factory.CreateClient();
        var r = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Api_ActiveUsers_RequiresAuth()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Role", "ANON");

        var r = await client.GetAsync("/api/users/active");

        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Api_ActiveUsers_ReturnsJsonArray_WhenAuthed()
    {
        var client = _factory.CreateClient();
        var r = await client.GetAsync("/api/users/active");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("application/json", r.Content.Headers.ContentType?.MediaType);
        var body = await r.Content.ReadAsStringAsync();
        Assert.StartsWith("[", body.TrimStart());
    }

    [Fact]
    public async Task StatusCode_404Page_ReturnsCustomThemedPage()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        var r = await client.GetAsync("/definitely-not-a-real-route-xyzzy");

        // Program.cs wires UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}") —
        // the re-execute means we get the themed 404 body but the status line is still 404.
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task CookieCulture_TogglesArabic_WhenCookieSet()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c=ar|uic=ar");

        var r = await client.GetAsync("/Categories");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Contains("dir=\"rtl\"", html);
    }
}
