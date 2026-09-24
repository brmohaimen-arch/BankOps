using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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

    private const string WebClientId = "bankops-web";
    private const string WebRedirectUri = "http://localhost:5173/callback";

    // Drives the real interactive login round-trip (authorize -> login form -> authorize again ->
    // token exchange, real PKCE) the same way a browser would, following redirects by hand since
    // the final hop targets apps/web's callback URL, which has no server listening during tests.
    public async Task<string> GetAccessTokenViaLoginAsync(string email, string password)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true };
        using var client = new HttpClient(handler);

        var verifier = GeneratePkceVerifier();
        var challenge = ComputeCodeChallenge(verifier);
        var authorizeUrl = $"{Authority}connect/authorize?client_id={WebClientId}&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(WebRedirectUri)}&scope=bankops.api%20openid" +
            $"&code_challenge={challenge}&code_challenge_method=S256&state=teststate";

        var authorizeResponse = await client.GetAsync(authorizeUrl);
        var loginPageUrl = new Uri(new Uri(Authority), authorizeResponse.Headers.Location!);
        var returnUrl = ExtractQueryParam(loginPageUrl.Query, "ReturnUrl");

        await client.PostAsync($"{Authority}login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["returnUrl"] = returnUrl,
        }));

        var authorizeAgainResponse = await client.GetAsync(authorizeUrl);
        var callbackLocation = authorizeAgainResponse.Headers.Location!;
        var code = ExtractQueryParam(callbackLocation.Query, "code");

        var tokenResponse = await client.PostAsync($"{Authority}connect/token", new FormUrlEncodedContent(
        [
            new("grant_type", "authorization_code"),
            new("code", code),
            new("redirect_uri", WebRedirectUri),
            new("client_id", WebClientId),
            new("code_verifier", verifier),
        ]));

        tokenResponse.EnsureSuccessStatusCode();
        var body = await tokenResponse.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    private static string GeneratePkceVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string ComputeCodeChallenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string ExtractQueryParam(string query, string key)
    {
        foreach (var pair in query.TrimStart('?').Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && Uri.UnescapeDataString(parts[0]) == key)
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        throw new InvalidOperationException($"Query parameter '{key}' not found in '{query}'.");
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
