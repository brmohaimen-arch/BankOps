using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace BankOps.Modules.Audit.Api;

// Read access restricted to admin — data governance: audit is its own classification tier
// (BankOps_03_Nonfunctional_Requirements.md, "Data governance").
[ApiController]
[Route("api/v1/audit")]
[Authorize(Roles = "admin")]
public class AuditController(NpgsqlDataSource dataSource) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? resourceType,
        [FromQuery] Guid? resourceId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200); // NFR-SEC-07: bounded payloads, no unbounded response

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, occurred_at, principal_ref, action, resource_type, resource_id,
                   correlation_id, safe_details
            FROM audit.entries
            WHERE (@resourceType::text IS NULL OR resource_type = @resourceType)
              AND (@resourceId::uuid IS NULL OR resource_id = @resourceId)
            ORDER BY occurred_at DESC, id DESC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue("resourceType", (object?)resourceType ?? DBNull.Value);
        command.Parameters.AddWithValue("resourceId", (object?)resourceId ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", limit);

        var results = new List<object>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new
            {
                id = reader.GetGuid(0),
                occurredAt = reader.GetDateTime(1),
                principalRef = reader.GetString(2),
                action = reader.GetString(3),
                resourceType = reader.GetString(4),
                resourceId = reader.IsDBNull(5) ? (Guid?)null : reader.GetGuid(5),
                correlationId = reader.GetString(6),
                safeDetails = reader.GetString(7),
            });
        }

        return Ok(results);
    }
}
