namespace BankOps.Api.Secrets;

// Dev-only: reads from IConfiguration, which surfaces `dotnet user-secrets` in Development and
// environment variables elsewhere. Never reads appsettings.json directly for secret values —
// user-secrets/env vars only, so a secret can never be accidentally committed inside a checked-in
// settings file.
public class ConfigurationSecretResolver(IConfiguration configuration) : ISecretResolver
{
    public ValueTask<string> ResolveAsync(string secretName, CancellationToken cancellationToken = default)
    {
        var value = configuration[$"Secrets:{secretName}"];
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(
                $"Secret '{secretName}' is not configured. " +
                $"For local dev: dotnet user-secrets set \"Secrets:{secretName}\" \"<value>\"");
        }

        return ValueTask.FromResult(value);
    }
}
