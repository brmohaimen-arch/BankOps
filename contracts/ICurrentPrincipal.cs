namespace BankOps.Contracts;

// A platform primitive per BankOps_05_Backend_Design.md: "a module may depend on platform
// primitives (IClock, IIdGenerator, ICurrentPrincipal, ...) and another module's published
// contract, never its implementation or tables." Modules (settings, audit, ...) depend only on
// this interface; the concrete implementation lives in apps/api (the composition root) since it's
// the only layer that knows about HttpContext.
public interface ICurrentPrincipal
{
    string Subject { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool IsInRole(string role);
}
