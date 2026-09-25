using Npgsql;

namespace BankOps.DbMigrator.Seeds;

// Synthetic catalog data for local dev and demos — `dotnet run -- seed`. Never runs as part of
// `up`. Every site is labelled "Synthetic" so seeded rows can't be mistaken for a real inventory in
// a screenshot. Only runs against an empty catalog, so it never mixes into data someone entered by
// hand, and running it twice is a no-op.
//
// Writes straight to the catalog tables (this tool already owns schema DDL), so it bypasses
// modules/catalog's API and therefore its audit trail — acceptable for disposable dev data only.
// The graph below is acyclic by construction; CatalogSeedTests re-checks that with the same
// DependencyGraph logic the publish endpoint uses.
public static class CatalogSyntheticData
{
    private record Service(string Code, string Name, string Owner, string Criticality, string Site);

    private static readonly Service[] Services =
    [
        new("CORE-BANKING", "Core banking platform", "team:core-banking", "CRITICAL", "Synthetic DC-1"),
        new("PAYMENTS-HUB", "Payments hub", "team:payments", "CRITICAL", "Synthetic DC-1"),
        new("CARD-SWITCH", "Card switch", "team:cards", "CRITICAL", "Synthetic DC-1"),
        new("SWIFT-GATEWAY", "SWIFT gateway", "team:payments", "CRITICAL", "Synthetic DC-1"),
        new("HSM-CLUSTER", "Hardware security modules", "team:security", "CRITICAL", "Synthetic DC-1"),
        new("INTERNET-BANKING", "Internet banking", "team:digital", "HIGH", "Synthetic DC-1"),
        new("MOBILE-BANKING", "Mobile banking", "team:digital", "HIGH", "Synthetic DC-1"),
        new("ATM-NETWORK", "ATM network", "team:cards", "HIGH", "Synthetic DC-2"),
        new("API-GATEWAY", "API gateway", "team:platform", "HIGH", "Synthetic DC-1"),
        new("DIRECTORY", "Directory / identity services", "team:infrastructure", "HIGH", "Synthetic DC-1"),
        new("AML-SCREENING", "AML screening", "team:compliance", "HIGH", "Synthetic DC-2"),
        new("SMS-GATEWAY", "SMS / OTP gateway", "team:digital", "MEDIUM", "Synthetic DC-2"),
        new("CRM", "Customer relationship management", "team:business-apps", "MEDIUM", "Synthetic DC-2"),
        new("DATA-WAREHOUSE", "Data warehouse", "team:data", "LOW", "Synthetic DC-2"),
    ];

    // (source, target, relation, isCritical)
    private static readonly (string From, string To, string Relation, bool Critical)[] Dependencies =
    [
        ("INTERNET-BANKING", "API-GATEWAY", "DEPENDS_ON", true),
        ("INTERNET-BANKING", "DIRECTORY", "USES", false),
        ("MOBILE-BANKING", "API-GATEWAY", "DEPENDS_ON", true),
        ("MOBILE-BANKING", "SMS-GATEWAY", "USES", true),
        ("API-GATEWAY", "CORE-BANKING", "DEPENDS_ON", true),
        ("API-GATEWAY", "DIRECTORY", "DEPENDS_ON", true),
        ("PAYMENTS-HUB", "CORE-BANKING", "DEPENDS_ON", true),
        ("PAYMENTS-HUB", "SWIFT-GATEWAY", "CONNECTS_TO", true),
        ("PAYMENTS-HUB", "AML-SCREENING", "USES", true),
        ("SWIFT-GATEWAY", "HSM-CLUSTER", "DEPENDS_ON", true),
        ("CARD-SWITCH", "CORE-BANKING", "DEPENDS_ON", true),
        ("CARD-SWITCH", "HSM-CLUSTER", "DEPENDS_ON", true),
        ("ATM-NETWORK", "CARD-SWITCH", "CONNECTS_TO", true),
        ("AML-SCREENING", "CORE-BANKING", "USES", false),
        ("CRM", "CORE-BANKING", "USES", false),
        ("DATA-WAREHOUSE", "CORE-BANKING", "USES", false),
        ("DATA-WAREHOUSE", "CRM", "USES", false),
    ];

    public static int ServiceCount => Services.Length;
    public static int DependencyCount => Dependencies.Length;

    // Returns the number of services inserted (0 when the catalog already had data).
    public static async Task<int> SeedAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var check = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM catalog.services);", connection, transaction))
        {
            if ((bool)(await check.ExecuteScalarAsync(cancellationToken))!)
            {
                return 0;
            }
        }

        var ids = new Dictionary<string, Guid>();
        foreach (var service in Services)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO catalog.services (code, name, owner_ref, criticality, environment, site)
                VALUES (@code, @name, @owner, @criticality, 'PROD', @site)
                RETURNING id;
                """, connection, transaction);
            insert.Parameters.AddWithValue("code", service.Code);
            insert.Parameters.AddWithValue("name", service.Name);
            insert.Parameters.AddWithValue("owner", service.Owner);
            insert.Parameters.AddWithValue("criticality", service.Criticality);
            insert.Parameters.AddWithValue("site", service.Site);
            ids[service.Code] = (Guid)(await insert.ExecuteScalarAsync(cancellationToken))!;
        }

        // Same versioning model modules/catalog's publish endpoint uses: a service with
        // dependencies has them as its version-2 set (version 1 = registered, nothing published).
        foreach (var (from, to, relation, critical) in Dependencies)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO catalog.dependencies (service_id, target_kind, target_id, relation, is_critical, published_version)
                VALUES (@from, 'SERVICE', @to, @relation, @critical, 2);
                """, connection, transaction);
            insert.Parameters.AddWithValue("from", ids[from]);
            insert.Parameters.AddWithValue("to", ids[to]);
            insert.Parameters.AddWithValue("relation", relation);
            insert.Parameters.AddWithValue("critical", critical);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var bump = new NpgsqlCommand("""
            UPDATE catalog.services SET graph_version = 2
            WHERE id IN (SELECT DISTINCT service_id FROM catalog.dependencies);
            """, connection, transaction))
        {
            await bump.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Services.Length;
    }
}
