using System.Diagnostics;
using System.Text.Json;
using BankOps.Contracts;
using BankOps.Modules.Audit.Contracts;
using BankOps.Modules.Settings.Contracts;
using BankOps.Modules.Settings.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankOps.Modules.Settings.Api;

[ApiController]
[Route("api/v1/settings")]
[Authorize] // any authenticated principal may read; writes are further restricted below
public class SettingsController(
    SettingsRepository repository,
    IAuditWriter auditWriter,
    ICurrentPrincipal currentPrincipal) : ControllerBase
{
    [HttpGet("{namespace}")]
    public async Task<IActionResult> List(string @namespace, CancellationToken cancellationToken)
    {
        var settings = await repository.ListAsync(@namespace, cancellationToken);
        return Ok(settings);
    }

    public record UpsertRequest(JsonElement Value, long? ExpectedVersion);

    [HttpPut("{namespace}/{key}")]
    [Authorize(Roles = "admin")] // FR-003/FR-006: settings writes are a platform-admin action
    public async Task<IActionResult> Upsert(
        string @namespace, string key, [FromBody] UpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await repository.UpsertAsync(
            @namespace, key, request.Value.GetRawText(), request.ExpectedVersion,
            currentPrincipal.Subject, cancellationToken);

        if (result.Outcome == UpsertOutcome.VersionConflict)
        {
            return Conflict(new
            {
                code = "SETTING_VERSION_CONFLICT",
                detail = "The setting was changed by someone else. Re-fetch it and retry with the current version.",
            });
        }

        var correlationId = Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
        await auditWriter.WriteAsync(new AuditEntry(
            PrincipalRef: currentPrincipal.Subject,
            Action: result.Outcome == UpsertOutcome.Created ? "settings.create" : "settings.update",
            ResourceType: "platform.app_settings",
            ResourceId: null, // app_settings uses a composite (namespace, key) identity, not a single uuid
            CorrelationId: correlationId,
            SafeDetails: new { @namespace, key, newVersion = result.NewVersion }),
            cancellationToken);

        return Ok(new { @namespace, key, version = result.NewVersion });
    }
}
