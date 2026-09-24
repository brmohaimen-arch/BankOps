namespace BankOps.Api.FeatureFlags;

public class ConfigurationFeatureFlagService(IConfiguration configuration) : IFeatureFlagService
{
    public bool IsEnabled(string flagName, string environment, IEnumerable<string> userRoles)
    {
        var section = configuration.GetSection($"FeatureFlags:{flagName}");
        if (!section.Exists() || !section.GetValue("Enabled", false))
        {
            return false;
        }

        var allowedEnvironments = section.GetSection("Environments").Get<string[]>();
        if (allowedEnvironments is { Length: > 0 } &&
            !allowedEnvironments.Contains(environment, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var allowedRoles = section.GetSection("Roles").Get<string[]>();
        if (allowedRoles is { Length: > 0 } &&
            !userRoles.Any(role => allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }
}
