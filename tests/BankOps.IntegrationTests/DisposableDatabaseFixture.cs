using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BankOps.IntegrationTests;

// A fresh, uniquely-named Postgres database per test run: created, migrated, and dropped —
// never the persistent bankops_dev used for manual local dev. Admin connection prefers real
// environment variables (what CI's postgres service container sets) and falls back to the
// repo-root .env's bankops_dev credentials for local runs, so the same code works in both places
// without a #if.
//
// Known simplification: locally this is still the same Postgres *server* as dev (just a
// throwaway database on it), not full network isolation — that needs Docker, which isn't
// installed on this dev machine (same constraint as the Valkey/cache decision). CI gets genuine
// isolation via a disposable postgres:18 service container; see .github/workflows/ci.yml.
public class DisposableDatabaseFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"bankops_test_{Guid.NewGuid():N}";
    private string _adminConnectionString = string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var (host, port, user, password) = ResolveAdminCredentials();

        _adminConnectionString = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = "postgres",
            Username = user,
            Password = password,
        }.ConnectionString;

        await using (var connection = new NpgsqlConnection(_adminConnectionString))
        {
            await connection.OpenAsync();
            await using var createCommand = connection.CreateCommand();
            // Database names can't be parameterized; the value is our own Guid, not external input.
            createCommand.CommandText = $"CREATE DATABASE \"{_databaseName}\" OWNER \"{user}\";";
            await createCommand.ExecuteNonQueryAsync();
        }

        ConnectionString = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = _databaseName,
            Username = user,
            Password = password,
        }.ConnectionString;

        var services = new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddPostgres()
                .WithGlobalConnectionString(ConnectionString)
                .ScanIn(typeof(BankOps.DbMigrator.Migrations.M0001_InitialSchema).Assembly).For.Migrations())
            .BuildServiceProvider(false);

        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
    }

    public async Task DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var dropCommand = connection.CreateCommand();
        // WITH (FORCE) (PG13+) disconnects any lingering sessions instead of failing the drop.
        dropCommand.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE);";
        await dropCommand.ExecuteNonQueryAsync();
    }

    private static (string Host, int Port, string User, string Password) ResolveAdminCredentials()
    {
        var host = Environment.GetEnvironmentVariable("POSTGRES_ADMIN_HOST");
        var port = Environment.GetEnvironmentVariable("POSTGRES_ADMIN_PORT");
        var user = Environment.GetEnvironmentVariable("POSTGRES_ADMIN_USER");
        var password = Environment.GetEnvironmentVariable("POSTGRES_ADMIN_PASSWORD");

        if (host is null || user is null || password is null)
        {
            var dotEnvPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", ".env"));
            var dotEnv = File.Exists(dotEnvPath)
                ? File.ReadAllLines(dotEnvPath)
                    .Select(line => line.Split('=', 2))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim())
                : [];

            host ??= dotEnv.GetValueOrDefault("BANKOPS_DB_HOST", "localhost");
            port ??= dotEnv.GetValueOrDefault("BANKOPS_DB_PORT", "5432");
            user ??= dotEnv.GetValueOrDefault("BANKOPS_DB_USER")
                ?? throw new InvalidOperationException("No admin DB user found in env vars or .env.");
            password ??= dotEnv.GetValueOrDefault("BANKOPS_DB_PASSWORD")
                ?? throw new InvalidOperationException("No admin DB password found in env vars or .env.");
        }

        port ??= "5432";
        return (host, int.Parse(port), user, password);
    }
}
