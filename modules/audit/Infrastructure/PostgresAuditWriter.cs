using System.Text.Json;
using BankOps.Modules.Audit.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace BankOps.Modules.Audit.Infrastructure;

public class PostgresAuditWriter(NpgsqlDataSource dataSource) : IAuditWriter
{
    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO audit.entries
                (principal_ref, action, resource_type, resource_id, correlation_id, safe_details)
            VALUES
                (@principalRef, @action, @resourceType, @resourceId, @correlationId, @safeDetails);
            """;

        command.Parameters.AddWithValue("principalRef", entry.PrincipalRef);
        command.Parameters.AddWithValue("action", entry.Action);
        command.Parameters.AddWithValue("resourceType", entry.ResourceType);
        command.Parameters.AddWithValue("resourceId", (object?)entry.ResourceId ?? DBNull.Value);
        command.Parameters.AddWithValue("correlationId", entry.CorrelationId);
        command.Parameters.Add(new NpgsqlParameter("safeDetails", NpgsqlDbType.Jsonb)
        {
            Value = JsonSerializer.Serialize(entry.SafeDetails),
        });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
