namespace BankOps.Modules.Audit.Contracts;

// Every other module writes audit entries through this contract — never a direct insert into
// audit.entries from outside this module (BankOps_05_Backend_Design.md, "Authorization, audit and
// actions").
public interface IAuditWriter
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}

public record AuditEntry(
    string PrincipalRef,
    string Action,
    string ResourceType,
    Guid? ResourceId,
    string CorrelationId,
    object SafeDetails);
