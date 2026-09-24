using Npgsql;

namespace BankOps.IntegrationTests;

// Exercises the Phase 1 exit evidence "a migration runs up and down cleanly" against a genuinely
// disposable database, not the persistent bankops_dev used for manual local dev.
public class MigrationTests : IAsyncLifetime
{
    private readonly DisposableDatabaseFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Up_CreatesExpectedPhase1Tables()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        var expected = new[]
        {
            ("platform", "app_settings"),
            ("audit", "entries"),
            ("catalog", "services"),
            ("catalog", "dependencies"),
        };

        foreach (var (schema, table) in expected)
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT EXISTS (SELECT 1 FROM information_schema.tables " +
                "WHERE table_schema = @schema AND table_name = @table)";
            command.Parameters.AddWithValue("schema", schema);
            command.Parameters.AddWithValue("table", table);

            var exists = (bool)(await command.ExecuteScalarAsync())!;
            Assert.True(exists, $"Expected table {schema}.{table} to exist after migration up.");
        }
    }

    [Fact]
    public async Task NativeUuidv7_IsAvailable()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT uuidv7();";

        var result = await command.ExecuteScalarAsync();
        Assert.NotNull(result);
    }
}
