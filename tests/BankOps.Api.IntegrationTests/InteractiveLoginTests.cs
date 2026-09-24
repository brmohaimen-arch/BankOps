using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BankOps.Api.IntegrationTests;

// Automates the manual curl-driven PKCE flow used while building AuthorizationController.cs
// (2026-09-24): authorize -> login form -> authorize again -> token exchange -> the resulting
// token actually works against apps/api. See DevIdentityProviderFixture.GetAccessTokenViaLoginAsync.
public class InteractiveLoginTests : IClassFixture<DevIdentityProviderFixture>, IAsyncLifetime
{
    private readonly DevIdentityProviderFixture _devIdp;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public InteractiveLoginTests(DevIdentityProviderFixture devIdp) => _devIdp = devIdp;

    public Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("Oidc__Authority", _devIdp.Authority);
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
    public async Task LoggedInAdmin_TokenWorksAgainstApi()
    {
        var token = await _devIdp.GetAccessTokenViaLoginAsync("admin@bankops.dev", "admin123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/authprobe/admin-only");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WrongPassword_LoginRoundTripFailsBeforeAnyTokenIsIssued()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _devIdp.GetAccessTokenViaLoginAsync("admin@bankops.dev", "wrong-password"));
    }
}
