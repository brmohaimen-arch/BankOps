using System.Diagnostics;
using BankOps.Contracts;
using BankOps.Modules.Audit.Contracts;
using BankOps.Modules.Catalog.Domain;
using BankOps.Modules.Catalog.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankOps.Modules.Catalog.Api;

// FR-101 (register services with owner/site/environment/criticality) and FR-102 (versioned
// dependency graph, cycles rejected at publish). Reads are open to any authenticated principal;
// every write is admin-only and audited through modules/audit's published IAuditWriter.
[ApiController]
[Route("api/v1/catalog")]
[Authorize]
public class CatalogController(
    CatalogRepository repository,
    IAuditWriter auditWriter,
    ICurrentPrincipal currentPrincipal) : ControllerBase
{
    [HttpGet("services")]
    public async Task<IActionResult> ListServices([FromQuery] int limit = 200, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500); // NFR-SEC-07: bounded payloads
        return Ok(await repository.ListServicesAsync(limit, cancellationToken));
    }

    [HttpGet("services/{id:guid}")]
    public async Task<IActionResult> GetService(Guid id, CancellationToken cancellationToken)
    {
        var detail = await repository.GetServiceAsync(id, cancellationToken);
        return detail is null ? NotFound(new { code = "SERVICE_NOT_FOUND" }) : Ok(detail);
    }

    public record CreateServiceRequest(
        string? Code, string? Name, string? OwnerRef, string? Criticality, string? Environment, string? Site);

    [HttpPost("services")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateService(
        [FromBody] CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var errors = CatalogRules.ValidateNewService(
            request.Code, request.Name, request.Criticality, request.Environment, request.OwnerRef, request.Site);
        if (errors.Count > 0)
        {
            return BadRequest(new { code = "VALIDATION_FAILED", errors });
        }

        var result = await repository.CreateServiceAsync(new NewService(
            request.Code!,
            request.Name!.Trim(),
            NullIfBlank(request.OwnerRef),
            request.Criticality!,
            request.Environment!.Trim(),
            NullIfBlank(request.Site)), cancellationToken);

        if (result.Created is null)
        {
            return Conflict(new
            {
                code = "SERVICE_CODE_TAKEN",
                detail = $"A service with code '{request.Code}' is already registered.",
            });
        }

        await auditWriter.WriteAsync(new AuditEntry(
            PrincipalRef: currentPrincipal.Subject,
            Action: "catalog.service.create",
            ResourceType: "catalog.services",
            ResourceId: result.Created.Id,
            CorrelationId: CorrelationId(),
            SafeDetails: new { result.Created.Code, result.Created.Criticality, result.Created.Environment }),
            cancellationToken);

        return CreatedAtAction(nameof(GetService), new { id = result.Created.Id }, result.Created);
    }

    public record DependencyRequest(string? TargetKind, Guid TargetId, string? Relation, bool? IsCritical);

    public record PublishDependenciesRequest(long? ExpectedGraphVersion, IReadOnlyList<DependencyRequest>? Dependencies);

    // Publishes the service's complete outgoing dependency set as a new graph version (replace, not
    // patch — the set is what gets versioned). There is no separate draft/review step yet: this is
    // the "publish" half of FR-102, gated by admin role, version check and cycle check.
    [HttpPut("services/{id:guid}/dependencies")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> PublishDependencies(
        Guid id, [FromBody] PublishDependenciesRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ExpectedGraphVersion is null)
        {
            // Same rule as settings: no blind overwrites — read the current version first.
            errors["expectedGraphVersion"] = ["Required. Read the service first and send its graphVersion."];
        }

        var dependencies = request.Dependencies ?? [];
        if (dependencies.Count > CatalogRules.MaxDependenciesPerService)
        {
            errors["dependencies"] = [$"At most {CatalogRules.MaxDependenciesPerService} dependencies per service."];
        }

        for (var i = 0; i < dependencies.Count; i++)
        {
            var d = dependencies[i];
            if (d.TargetKind is null || !CatalogRules.TargetKinds.Contains(d.TargetKind))
            {
                errors[$"dependencies[{i}].targetKind"] = [$"Must be one of: {string.Join(", ", CatalogRules.TargetKinds)}."];
            }

            if (d.Relation is null || !CatalogRules.Relations.Contains(d.Relation))
            {
                errors[$"dependencies[{i}].relation"] = [$"Must be one of: {string.Join(", ", CatalogRules.Relations)}."];
            }

            if (d.TargetId == Guid.Empty)
            {
                errors[$"dependencies[{i}].targetId"] = ["Required."];
            }
        }

        var duplicates = dependencies
            .GroupBy(d => (d.TargetKind, d.TargetId, d.Relation))
            .Where(g => g.Count() > 1)
            .ToArray();
        if (duplicates.Length > 0)
        {
            errors["dependencies"] = ["The same target and relation appear more than once."];
        }

        if (errors.Count > 0)
        {
            return BadRequest(new { code = "VALIDATION_FAILED", errors });
        }

        var unsupported = dependencies
            .Select(d => d.TargetKind!)
            .Where(kind => !CatalogRules.SupportedTargetKinds.Contains(kind))
            .Distinct()
            .ToArray();
        if (unsupported.Length > 0)
        {
            return UnprocessableEntity(new
            {
                code = "TARGET_KIND_NOT_SUPPORTED_YET",
                detail = $"Only {string.Join(", ", CatalogRules.SupportedTargetKinds)} targets can be validated today. " +
                         $"Not yet supported: {string.Join(", ", unsupported)}.",
            });
        }

        var result = await repository.PublishDependenciesAsync(
            id,
            request.ExpectedGraphVersion!.Value,
            dependencies.Select(d => new NewDependency(d.TargetKind!, d.TargetId, d.Relation!, d.IsCritical ?? true)).ToArray(),
            cancellationToken);

        switch (result.Outcome)
        {
            case PublishOutcome.NotFound:
                return NotFound(new { code = "SERVICE_NOT_FOUND" });
            case PublishOutcome.VersionConflict:
                return Conflict(new
                {
                    code = "GRAPH_VERSION_CONFLICT",
                    detail = "The dependency set was published by someone else. Re-fetch the service and retry.",
                    currentGraphVersion = result.CurrentVersion,
                });
            case PublishOutcome.UnknownTargets:
                return UnprocessableEntity(new
                {
                    code = "DEPENDENCY_TARGET_NOT_FOUND",
                    detail = "One or more target services are not registered.",
                    targetIds = result.UnknownTargetIds,
                });
            case PublishOutcome.Cycle:
                return UnprocessableEntity(new
                {
                    code = "DEPENDENCY_CYCLE",
                    detail = $"Publishing would create a dependency cycle: {string.Join(" -> ", result.CyclePath!)}.",
                    cycle = result.CyclePath,
                });
        }

        await auditWriter.WriteAsync(new AuditEntry(
            PrincipalRef: currentPrincipal.Subject,
            Action: "catalog.dependencies.publish",
            ResourceType: "catalog.services",
            ResourceId: id,
            CorrelationId: CorrelationId(),
            SafeDetails: new { newGraphVersion = result.NewVersion, dependencyCount = dependencies.Count }),
            cancellationToken);

        return Ok(new { serviceId = id, graphVersion = result.NewVersion });
    }

    private string CorrelationId() => Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
