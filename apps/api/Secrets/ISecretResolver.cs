namespace BankOps.Api.Secrets;

// NFR-SEC-04: "Secrets stored in approved vault or encrypted secret service with rotation; DB
// stores references and non-secret metadata only." Every call site asks for a secret by logical
// name through this interface; swapping the backing store (dotnet user-secrets locally -> the
// bank's actual approved vault, D-04 territory) is a new implementation registered in DI, not a
// change anywhere secrets are consumed.
public interface ISecretResolver
{
    ValueTask<string> ResolveAsync(string secretName, CancellationToken cancellationToken = default);
}
