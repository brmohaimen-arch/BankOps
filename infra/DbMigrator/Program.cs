using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
LoadDotEnv(Path.Combine(repoRoot, ".env"));

var connectionString = new NpgsqlConnectionStringBuilder
{
    Host = RequireEnv("BANKOPS_DB_HOST"),
    Port = int.Parse(RequireEnv("BANKOPS_DB_PORT")),
    Database = RequireEnv("BANKOPS_DB_NAME"),
    Username = RequireEnv("BANKOPS_DB_USER"),
    Password = RequireEnv("BANKOPS_DB_PASSWORD"),
}.ConnectionString;

var direction = args.Length > 0 ? args[0].ToLowerInvariant() : "up";

using var serviceProvider = new ServiceCollection()
    .AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddPostgres()
        .WithGlobalConnectionString(connectionString)
        .ScanIn(typeof(Program).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddFluentMigratorConsole())
    .BuildServiceProvider(false);

using var scope = serviceProvider.CreateScope();
var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

switch (direction)
{
    case "up":
        runner.MigrateUp();
        break;
    case "down":
        // Rolls back every migration this runner knows about (all the way to version 0).
        // Fine for a dev-only tool with one migration; revisit with an explicit target version
        // once there is more than one migration to choose from.
        runner.MigrateDown(0);
        break;
    default:
        Console.Error.WriteLine($"Unknown direction '{direction}'. Use 'up' or 'down'.");
        Environment.Exit(1);
        break;
}

static string RequireEnv(string name) =>
    Environment.GetEnvironmentVariable(name)
    ?? throw new InvalidOperationException($"Missing required environment variable: {name}");

static void LoadDotEnv(string path)
{
    if (!File.Exists(path))
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();
        if (Environment.GetEnvironmentVariable(key) is null)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
