using System.Text.RegularExpressions;

namespace BankOps.Modules.Catalog.Domain;

// Field rules for catalog writes. The CHECK constraints in M0001_InitialSchema are the last line of
// defence; these exist so a bad request gets a 400 naming the field, not a 500 from a constraint
// violation.
public static partial class CatalogRules
{
    public static readonly IReadOnlyList<string> Criticalities = ["CRITICAL", "HIGH", "MEDIUM", "LOW"];
    public static readonly IReadOnlyList<string> TargetKinds = ["SERVICE", "ASSET", "ENDPOINT", "DATABASE"];
    public static readonly IReadOnlyList<string> Relations = ["DEPENDS_ON", "USES", "CONNECTS_TO"];

    // Only SERVICE targets can be validated today: there is no asset/endpoint/database registry to
    // check a target_id against yet, and the README requires targets be validated at publish (the
    // column is a polymorphic reference, not a SQL FK). Accepting unvalidated ids would publish
    // edges nothing can resolve.
    public static readonly IReadOnlyList<string> SupportedTargetKinds = ["SERVICE"];

    public const int MaxDependenciesPerService = 100;

    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]{1,63}$")]
    private static partial Regex CodePattern();

    public static Dictionary<string, string[]> ValidateNewService(
        string? code, string? name, string? criticality, string? environment, string? ownerRef, string? site)
    {
        var errors = new Dictionary<string, string[]>();

        if (code is null || !CodePattern().IsMatch(code))
        {
            errors["code"] = ["Required. 2-64 characters: uppercase letters, digits and hyphens, not starting with a hyphen."];
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            errors["name"] = ["Required, at most 200 characters."];
        }

        if (criticality is null || !Criticalities.Contains(criticality))
        {
            errors["criticality"] = [$"Must be one of: {string.Join(", ", Criticalities)}."];
        }

        if (string.IsNullOrWhiteSpace(environment) || environment.Length > 50)
        {
            errors["environment"] = ["Required, at most 50 characters."];
        }

        if (ownerRef is not null && ownerRef.Length > 200)
        {
            errors["ownerRef"] = ["At most 200 characters."];
        }

        if (site is not null && site.Length > 100)
        {
            errors["site"] = ["At most 100 characters."];
        }

        return errors;
    }
}
