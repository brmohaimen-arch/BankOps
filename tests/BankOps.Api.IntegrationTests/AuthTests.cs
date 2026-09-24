using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BankOps.Api.IntegrationTests;

// Automates the four-case object-level RBAC check that was previously run by hand with curl
// (see documents/PHASE1_CHECKLIST.md, 2026-09-24). Runs against the real JwtBearer/OIDC pipeline,
// not a substituted auth handler — see DevIdentityProviderFixture for why that distinction matters.
public class AuthTests : IClassFixture<DevIdentityProviderFixture>, IAsyncLifetime
{
    private readonly DevIdentityProviderFixture _devIdp;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public AuthTests(DevIdentityProviderFixture devIdp) => _devIdp = devIdp;

    public Task InitializeAsync()
    {
        // ConfigureAppConfiguration overlays on WithWebHostBuilder are not guaranteed to be visible
        // to Program.cs's own top-level code, which reads builder.Configuration synchronously
        // before the test host finishes wiring itself up — apps/api's `oidcAuthority` variable was
        // silently capturing the http://localhost:5026/ fallback regardless of this override.
        // Environment variables are read by WebApplication.CreateBuilder(args) itself as one of its
        // own default sources, which sidesteps that ordering problem entirely.
        Environment.SetEnvironmentVariable("Oidc__Authority", _devIdp.Authority);

        // apps/api opens a real NpgsqlDataSource at startup regardless of whether a given test
        // needs it (modules/settings, modules/audit). None of these tests touch DB-backed
        // endpoints, but Program.cs still requires a syntactically valid connection string to
        // start at all — a placeholder is enough (NpgsqlDataSource.Create doesn't connect
        // eagerly). Set explicitly rather than relying on local dotnet user-secrets, which exist
        // on this dev machine but not on a fresh CI runner or in SettingsAndAuditTests, which sets
        // its own real one — this test must not depend on running before or after that one.
        Environment.SetEnvironmentVariable(
            "Secrets__BankOpsDbConnectionString", "Host=localhost;Database=unused_by_these_tests");

        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task NoToken_WhoAmI_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/authprobe/whoami");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ViewerToken_WhoAmI_ReturnsOk()
    {
        var token = await _devIdp.GetAccessTokenAsync("bankops-dev-viewer", "dev-viewer-secret");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/authprobe/whoami");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ViewerToken_AdminOnly_ReturnsForbidden()
    {
        var token = await _devIdp.GetAccessTokenAsync("bankops-dev-viewer", "dev-viewer-secret");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/authprobe/admin-only");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminToken_AdminOnly_ReturnsOk()
    {
        var token = await _devIdp.GetAccessTokenAsync("bankops-dev-admin", "dev-admin-secret");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/authprobe/admin-only");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
