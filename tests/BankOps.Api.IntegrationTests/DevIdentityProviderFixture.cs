using System.Diagnostics;
using System.Text.Json;

namespace BankOps.Api.IntegrationTests;

// Runs the real infra/DevIdentityProvider as a real subprocess on a real port, the same way it's
// been run manually all session — not WebApplicationFactory's in-memory TestServer, because
// apps/api's JwtBearer needs a genuinely reachable HTTP Authority to fetch OIDC metadata/JWKS
// from. This is deliberate: the bug this whole test exists to catch (MapInboundClaims silently
// breaking role claims) lives in that exact real HTTP token-validation path — a substituted/fake
// auth handler would test around the bug, not for it.
public class DevIdentityProviderFixture : IAsyncLifetime
{
    private const int Port = 5099; // distinct from the port used when running it manually (5026)
    private Process? _process;

    public string Authority { get; } = $"http://localhost:{Port}/";

    public async Task InitializeAsync()
    {
        var projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "infra", "DevIdentityProvider", "BankOps.DevIdentityProvider.csproj"));

        _process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" --no-launch-profile --urls {Authority}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("Failed to start infra/DevIdentityProvider.");

        using var httpClient = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await httpClient.GetAsync($"{Authority}.well-known/openid-configuration");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Not up yet — keep polling.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("infra/DevIdentityProvider did not become ready within 30s.");
    }

    public async Task<string> GetAccessTokenAsync(string clientId, string clientSecret)
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsync($"{Authority}connect/token", new FormUrlEncodedContent(
        [
            new("grant_type", "client_credentials"),
            new("client_id", clientId),
            new("client_secret", clientSecret),
            new("scope", "bankops.api"),
        ]));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    public Task DisposeAsync()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(5000);
        }

        _process?.Dispose();
        return Task.CompletedTask;
    }
}
