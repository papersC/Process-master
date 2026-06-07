using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ESEMS.Tests.Integration;

/// <summary>
/// Locks the post-deploy "Access Denied" fix.
///
/// Unauthenticated access to a protected page must 302-redirect to the cookie
/// login page — NOT issue a Windows-Negotiate 401 that IIS renders as its raw
/// "401 — Access is denied" page. Program.cs sets DefaultChallengeScheme =
/// "Cookies" and an OnRedirectToLogin event that builds the URL from the live
/// request PathBase, so the redirect carries EXACTLY ONE /App prefix under the
/// IIS sub-application (neither doubled nor missing).
///
/// The factory only overrides the default *authenticate* scheme (TestAuth), not
/// DefaultChallengeScheme, so an anonymous request here exercises the real
/// cookie challenge + OnRedirectToLogin event.
/// </summary>
public class LoginRedirectTests : IClassFixture<EsemsTestFactory>
{
    private readonly EsemsTestFactory _factory;
    public LoginRedirectTests(EsemsTestFactory factory) { _factory = factory; }

    private HttpClient Anon()
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-Role", "ANON");
        return c;
    }

    [Fact]
    public async Task Unauthenticated_Redirects_To_Login_With_ReturnUrl()
    {
        var r = await Anon().GetAsync("/Improvements");

        Assert.Equal(HttpStatusCode.Found, r.StatusCode);            // 302, not 401
        var loc = r.Headers.Location!.OriginalString;
        Assert.StartsWith("/Account/Login", loc);
        Assert.Contains("returnUrl=%2FImprovements", loc);          // carried back to the target
    }

    [Fact]
    public async Task Unauthenticated_Redirect_Keeps_Exactly_One_App_Prefix()
    {
        // Hitting the route THROUGH the /App sub-app path: UsePathBase strips
        // /App into PathBase, and the event mirrors that one prefix back.
        var r = await Anon().GetAsync("/App/Improvements");

        Assert.Equal(HttpStatusCode.Found, r.StatusCode);
        var loc = r.Headers.Location!.OriginalString;
        Assert.StartsWith("/App/Account/Login", loc);
        Assert.DoesNotContain("/App/App/", loc);                    // not double-prefixed
        Assert.Contains("returnUrl=%2FApp%2FImprovements", loc);
    }

    [Fact]
    public async Task AccessDenied_Page_Shows_The_SignedIn_Identity()
    {
        // A signed-in user lacking a permission lands here. The page must make it
        // obvious they ARE still signed in (it's a 403, not an expired session).
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Test-Role", "VIEWER");
        c.DefaultRequestHeaders.Add("X-Test-Name", "Khaled Almiani");
        c.DefaultRequestHeaders.Add("X-Test-Perms", "Asset.View");

        var r = await c.GetAsync("/Account/AccessDenied");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Contains("You are signed in as", html);              // identity label rendered
        Assert.Contains("Khaled Almiani", html);                    // ...with the actual user
    }

    // Render the shared layout (via the AccessDenied page) and inspect the
    // sidebar: action/cross-module links must appear only when the user holds
    // the permission their target controller requires.
    private async Task<string> SidebarHtmlFor(string perms)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Test-Role", "VIEWER");
        c.DefaultRequestHeaders.Add("X-Test-Perms", perms);
        return await (await c.GetAsync("/Account/AccessDenied")).Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Sidebar_Hides_Links_The_User_Cannot_Access()
    {
        var html = await SidebarHtmlFor("Asset.View"); // asset viewer: none of the below

        Assert.DoesNotContain("/Workflow/PendingApprovals", html);  // needs CanApprove
        Assert.DoesNotContain("/WorkloadAnalysis/Create", html);    // needs Workload.Create
        Assert.DoesNotContain("/WorkloadAnalysis/Config", html);    // needs Workload.Edit
        Assert.DoesNotContain("/ChangeRequests", html);             // needs ChangeRequest.View
        Assert.DoesNotContain("/KpiLibrary", html);                 // needs Improvement.View
    }

    [Fact]
    public async Task Sidebar_Shows_Links_The_User_Can_Access()
    {
        var html = await SidebarHtmlFor("*.*"); // full access sees them all

        Assert.Contains("/Workflow/PendingApprovals", html);
        Assert.Contains("/WorkloadAnalysis/Create", html);
        Assert.Contains("/WorkloadAnalysis/Config", html);
        Assert.Contains("/ChangeRequests", html);
        Assert.Contains("/KpiLibrary", html);
    }
}
