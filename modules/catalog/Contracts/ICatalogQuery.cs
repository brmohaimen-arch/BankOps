namespace BankOps.Modules.Catalog.Contracts;

// The published read contract for the catalog (modules/catalog/README.md, "Boundaries"): monitoring
// and incidents read services and the dependency graph through this interface, never by joining
// against the catalog schema. Health is deliberately absent — catalog owns what exists and how it
// connects, not whether it is up; that is modules/monitoring's evidence to supply (Phase 3).
public interface ICatalogQuery
{
    Task<IReadOnlyList<ServiceSummaryDto>> ListServicesAsync(int limit, CancellationToken cancellationToken = default);

    Task<ServiceDetailDto?> GetServiceAsync(Guid id, CancellationToken cancellationToken = default);
}

public record ServiceSummaryDto(
    Guid Id,
    string Code,
    string Name,
    string? OwnerRef,
    string Criticality,
    string Environment,
    string? Site,
    long GraphVersion, // the service's currently published dependency-set version
    DateTimeOffset UpdatedAt);

// Outgoing edges at the service's current published graph version. TargetCode/TargetName are only
// resolvable for SERVICE targets today — the asset/endpoint/database registries don't exist yet.
public record DependencyDto(
    string TargetKind,
    Guid TargetId,
    string? TargetCode,
    string? TargetName,
    string Relation,
    bool IsCritical);

// Incoming edges: other services whose current published dependency set points at this one.
public record DependentDto(
    Guid ServiceId,
    string Code,
    string Name,
    string Relation,
    bool IsCritical);

public record ServiceDetailDto(
    ServiceSummaryDto Service,
    IReadOnlyList<DependencyDto> Dependencies,
    IReadOnlyList<DependentDto> Dependents);
