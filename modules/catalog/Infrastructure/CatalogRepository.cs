using BankOps.Modules.Catalog.Contracts;
using BankOps.Modules.Catalog.Domain;
using Npgsql;
using NpgsqlTypes;

namespace BankOps.Modules.Catalog.Infrastructure;

// Raw Npgsql against the catalog schema (created in M0001_InitialSchema), same approach as
// modules/settings. Versioning model: catalog.services.graph_version is the service's currently
// published dependency-set version; catalog.dependencies rows are immutable per published_version.
// A new service starts at graph_version 1 with no rows, i.e. "published, no dependencies". Publishing
// inserts a complete new set at graph_version + 1 — earlier versions stay as history.
public class CatalogRepository(NpgsqlDataSource dataSource) : ICatalogQuery
{
    // One lock for the whole graph: a cycle check is only valid if no other publish changes the
    // edges between the check and the commit. Publishes are rare admin actions, so serializing them
    // costs nothing. Arbitrary constant, only needs to be unique among this database's advisory locks.
    private const long GraphPublishLockKey = 0x_CA7A_1006;

    private const string ServiceColumns =
        "id, code, name, owner_ref, criticality, environment, site, graph_version, updated_at";

    public async Task<IReadOnlyList<ServiceSummaryDto>> ListServicesAsync(
        int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {ServiceColumns}
            FROM catalog.services
            ORDER BY array_position(ARRAY['CRITICAL','HIGH','MEDIUM','LOW'], criticality), code
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue("limit", limit);

        var results = new List<ServiceSummaryDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadService(reader));
        }

        return results;
    }

    public async Task<ServiceDetailDto?> GetServiceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        ServiceSummaryDto service;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"SELECT {ServiceColumns} FROM catalog.services WHERE id = @id;";
            command.Parameters.AddWithValue("id", id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            service = ReadService(reader);
        }

        var dependencies = new List<DependencyDto>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT d.target_kind, d.target_id, t.code, t.name, d.relation, d.is_critical
                FROM catalog.dependencies d
                LEFT JOIN catalog.services t ON d.target_kind = 'SERVICE' AND t.id = d.target_id
                WHERE d.service_id = @id AND d.published_version = @version
                ORDER BY d.is_critical DESC, t.code NULLS LAST, d.target_id;
                """;
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("version", service.GraphVersion);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                dependencies.Add(new DependencyDto(
                    reader.GetString(0),
                    reader.GetGuid(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetString(4),
                    reader.GetBoolean(5)));
            }
        }

        var dependents = new List<DependentDto>();
        await using (var command = connection.CreateCommand())
        {
            // Only each source service's *current* published version counts — an edge that was
            // removed in a later publish must not keep showing up as a dependent.
            command.CommandText = """
                SELECT s.id, s.code, s.name, d.relation, d.is_critical
                FROM catalog.dependencies d
                JOIN catalog.services s ON s.id = d.service_id AND d.published_version = s.graph_version
                WHERE d.target_kind = 'SERVICE' AND d.target_id = @id
                ORDER BY d.is_critical DESC, s.code;
                """;
            command.Parameters.AddWithValue("id", id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                dependents.Add(new DependentDto(
                    reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4)));
            }
        }

        return new ServiceDetailDto(service, dependencies, dependents);
    }

    public async Task<CreateServiceResult> CreateServiceAsync(
        NewService service, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO catalog.services (code, name, owner_ref, criticality, environment, site)
            VALUES (@code, @name, @ownerRef, @criticality, @environment, @site)
            ON CONFLICT (code) DO NOTHING
            RETURNING {ServiceColumns};
            """;
        command.Parameters.AddWithValue("code", service.Code);
        command.Parameters.AddWithValue("name", service.Name);
        command.Parameters.Add(NullableText("ownerRef", service.OwnerRef));
        command.Parameters.AddWithValue("criticality", service.Criticality);
        command.Parameters.AddWithValue("environment", service.Environment);
        command.Parameters.Add(NullableText("site", service.Site));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new CreateServiceResult(ReadService(reader))
            : new CreateServiceResult(null); // code already taken
    }

    public async Task<PublishResult> PublishDependenciesAsync(
        Guid serviceId,
        long expectedGraphVersion,
        IReadOnlyList<NewDependency> dependencies,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key);", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue("key", GraphPublishLockKey);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        long currentVersion;
        await using (var command = new NpgsqlCommand(
            "SELECT graph_version FROM catalog.services WHERE id = @id FOR UPDATE;", connection, transaction))
        {
            command.Parameters.AddWithValue("id", serviceId);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null)
            {
                return PublishResult.NotFound();
            }

            currentVersion = (long)result;
        }

        if (currentVersion != expectedGraphVersion)
        {
            return PublishResult.VersionConflict(currentVersion);
        }

        var targetIds = dependencies.Select(d => d.TargetId).Distinct().ToArray();
        var existing = new Dictionary<Guid, string>();
        await using (var command = new NpgsqlCommand(
            "SELECT id, code FROM catalog.services WHERE id = ANY(@ids);", connection, transaction))
        {
            command.Parameters.AddWithValue("ids", targetIds);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                existing[reader.GetGuid(0)] = reader.GetString(1);
            }
        }

        var unknownTargets = targetIds.Where(id => !existing.ContainsKey(id)).ToArray();
        if (unknownTargets.Length > 0)
        {
            return PublishResult.UnknownTargets(unknownTargets);
        }

        var edges = new Dictionary<Guid, List<Guid>>();
        var codes = new Dictionary<Guid, string>();
        await using (var command = new NpgsqlCommand("""
            SELECT s.id, s.code, d.target_id
            FROM catalog.services s
            LEFT JOIN catalog.dependencies d
              ON d.service_id = s.id AND d.published_version = s.graph_version AND d.target_kind = 'SERVICE';
            """, connection, transaction))
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var source = reader.GetGuid(0);
                codes[source] = reader.GetString(1);
                if (!reader.IsDBNull(2))
                {
                    if (!edges.TryGetValue(source, out var targets))
                    {
                        edges[source] = targets = [];
                    }

                    targets.Add(reader.GetGuid(2));
                }
            }
        }

        var cycle = DependencyGraph.FindCycle(
            edges.ToDictionary(e => e.Key, e => (IReadOnlyCollection<Guid>)e.Value), serviceId, targetIds);
        if (cycle is not null)
        {
            return PublishResult.Cycle(cycle.Select(id => codes.GetValueOrDefault(id, id.ToString())).ToArray());
        }

        var newVersion = currentVersion + 1;
        foreach (var dependency in dependencies)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO catalog.dependencies (service_id, target_kind, target_id, relation, is_critical, published_version)
                VALUES (@serviceId, @targetKind, @targetId, @relation, @isCritical, @version);
                """, connection, transaction);
            insert.Parameters.AddWithValue("serviceId", serviceId);
            insert.Parameters.AddWithValue("targetKind", dependency.TargetKind);
            insert.Parameters.AddWithValue("targetId", dependency.TargetId);
            insert.Parameters.AddWithValue("relation", dependency.Relation);
            insert.Parameters.AddWithValue("isCritical", dependency.IsCritical);
            insert.Parameters.AddWithValue("version", newVersion);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var update = new NpgsqlCommand(
            "UPDATE catalog.services SET graph_version = @version, updated_at = now() WHERE id = @id;",
            connection, transaction))
        {
            update.Parameters.AddWithValue("version", newVersion);
            update.Parameters.AddWithValue("id", serviceId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return PublishResult.Published(newVersion);
    }

    private static ServiceSummaryDto ReadService(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.GetInt64(7),
        reader.GetFieldValue<DateTimeOffset>(8));

    // Same 42P08 gotcha as SettingsRepository: a bare DBNull needs an explicit parameter type.
    private static NpgsqlParameter NullableText(string name, string? value) =>
        new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };
}

public record NewService(
    string Code, string Name, string? OwnerRef, string Criticality, string Environment, string? Site);

public record CreateServiceResult(ServiceSummaryDto? Created);

public record NewDependency(string TargetKind, Guid TargetId, string Relation, bool IsCritical);

public enum PublishOutcome
{
    Published,
    NotFound,
    VersionConflict,
    UnknownTargets,
    Cycle,
}

public record PublishResult(
    PublishOutcome Outcome,
    long? NewVersion = null,
    long? CurrentVersion = null,
    IReadOnlyList<Guid>? UnknownTargetIds = null,
    IReadOnlyList<string>? CyclePath = null)
{
    public static PublishResult Published(long newVersion) => new(PublishOutcome.Published, NewVersion: newVersion);
    public static PublishResult NotFound() => new(PublishOutcome.NotFound);
    public static PublishResult VersionConflict(long current) => new(PublishOutcome.VersionConflict, CurrentVersion: current);
    public static PublishResult UnknownTargets(IReadOnlyList<Guid> ids) => new(PublishOutcome.UnknownTargets, UnknownTargetIds: ids);
    public static PublishResult Cycle(IReadOnlyList<string> path) => new(PublishOutcome.Cycle, CyclePath: path);
}
