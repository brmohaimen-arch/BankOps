using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BankOps.IntegrationTests;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BankOps.Api.IntegrationTests;

// Exercises modules/settings and modules/audit through apps/api end-to-end: real disposable
// Postgres database (DisposableDatabaseFixture, shared with tests/BankOps.IntegrationTests), real
// OIDC tokens (DevIdentityProviderFixture), real WebApplicationFactory-hosted API. This is the
// automated version of the manual curl sequence run while building the modules (2026-09-24):
// create -> read -> wrong-version conflict -> correct-version update -> audit trail -> RBAC.
public class SettingsAndAuditTests : IClassFixture<DevIdentityProviderFixture>, IAsyncLifetime
{
    private readonly DevIdentityProviderFixture _devIdp;
    private readonly DisposableDatabaseFixture _database = new();
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public SettingsAndAuditTests(DevIdentityProviderFixture devIdp) => _devIdp = devIdp;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        // See AuthTests/README.md: ConfigureAppConfiguration overlays aren't visible to
        // Program.cs's synchronous config reads in time — environment variables are.
        Environment.SetEnvironmentVariable("Oidc__Authority", _devIdp.Authority);
        Environment.SetEnvironmentVariable("Secrets__BankOpsDbConnectionString", _database.ConnectionString);

        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory?.Dispose();
        await _database.DisposeAsync();
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string clientId, string clientSecret)
    {
        var token = await _devIdp.GetAccessTokenAsync(clientId, clientSecret);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client;
    }

    [Fact]
    public async Task FullLifecycle_CreateReadConflictUpdate_ThenAuditTrailReflectsBoth()
    {
        var admin = await AuthenticatedClientAsync("bankops-dev-admin", "dev-admin-secret");

        var createResponse = await admin.PutAsJsonAsync(
            "/api/v1/settings/branding/primaryColor", new { value = "#0033AA" });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var listResponse = await admin.GetFromJsonAsync<List<SettingDto>>("/api/v1/settings/branding");
        var created = Assert.Single(listResponse!);
        Assert.Equal(1, created.Version);

        var conflictResponse = await admin.PutAsJsonAsync(
            "/api/v1/settings/branding/primaryColor", new { value = "#FF0000", expectedVersion = 99 });
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        var updateResponse = await admin.PutAsJsonAsync(
            "/api/v1/settings/branding/primaryColor", new { value = "#FF0000", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var auditResponse = await admin.GetFromJsonAsync<List<AuditEntryDto>>(
            "/api/v1/audit?resourceType=platform.app_settings");
        Assert.Equal(2, auditResponse!.Count);
        Assert.Contains(auditResponse, e => e.Action == "settings.create");
        Assert.Contains(auditResponse, e => e.Action == "settings.update");
    }

    [Fact]
    public async Task ViewerCannotWriteSettingsOrReadAudit()
    {
        var viewer = await AuthenticatedClientAsync("bankops-dev-viewer", "dev-viewer-secret");

        var writeResponse = await viewer.PutAsJsonAsync(
            "/api/v1/settings/branding/primaryColor", new { value = "#000000" });
        Assert.Equal(HttpStatusCode.Forbidden, writeResponse.StatusCode);

        var auditResponse = await viewer.GetAsync("/api/v1/audit");
        Assert.Equal(HttpStatusCode.Forbidden, auditResponse.StatusCode);
    }

    private record SettingDto(string Namespace, string Key, string Value, long Version);
    private record AuditEntryDto(Guid Id, string Action, string ResourceType);
}
