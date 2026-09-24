using BankOps.Modules.Settings.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace BankOps.Modules.Settings.Infrastructure;

// FR-006: "Editing one namespace does not overwrite another; secrets never returned by settings
// GET." Namespace isolation comes from the WHERE clause below, not application-level trust.
// Optimistic concurrency (the `version` column) matches the pattern BankOps_06_Database_Design.md
// uses for incidents: no expected version supplied for an already-existing key is treated as a
// conflict, forcing callers to read before they write.
public class SettingsRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<SettingDto>> ListAsync(
        string @namespace, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT namespace, setting_key, value::text, version, updated_at, updated_by
            FROM platform.app_settings
            WHERE namespace = @namespace
            ORDER BY setting_key;
            """;
        command.Parameters.AddWithValue("namespace", @namespace);

        var results = new List<SettingDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SettingDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetString(5)));
        }

        return results;
    }

    public async Task<UpsertResult> UpsertAsync(
        string @namespace,
        string key,
        string valueJson,
        long? expectedVersion,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO platform.app_settings (namespace, setting_key, value, version, updated_at, updated_by)
            VALUES (@namespace, @key, @value, 1, now(), @updatedBy)
            ON CONFLICT (namespace, setting_key) DO UPDATE
              SET value = @value,
                  version = platform.app_settings.version + 1,
                  updated_at = now(),
                  updated_by = @updatedBy
              WHERE @expectedVersion IS NOT NULL
                AND platform.app_settings.version = @expectedVersion
            RETURNING version, (xmax = 0) AS inserted;
            """;

        command.Parameters.AddWithValue("namespace", @namespace);
        command.Parameters.AddWithValue("key", key);
        command.Parameters.Add(new NpgsqlParameter("value", NpgsqlDbType.Jsonb) { Value = valueJson });
        command.Parameters.AddWithValue("updatedBy", updatedBy);
        // AddWithValue can't infer a type from a bare DBNull (the common case: no version on
        // first create), and Postgres can't type-check "@expectedVersion IS NOT NULL" without
        // one — "42P08: could not determine data type of parameter". Explicit type required.
        command.Parameters.Add(new NpgsqlParameter("expectedVersion", NpgsqlDbType.Bigint)
        {
            Value = (object?)expectedVersion ?? DBNull.Value,
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new UpsertResult(UpsertOutcome.VersionConflict, null);
        }

        var newVersion = reader.GetInt64(0);
        var wasInsert = reader.GetBoolean(1);
        return new UpsertResult(wasInsert ? UpsertOutcome.Created : UpsertOutcome.Updated, newVersion);
    }
}
