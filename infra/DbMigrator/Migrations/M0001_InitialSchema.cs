using FluentMigrator;

namespace BankOps.DbMigrator.Migrations;

// Scoped to what Phase 1 actually needs: platform (settings), audit, catalog.
// monitoring/events/incidents/knowledge are Phase 3-4 — do not add their tables here.
// DDL is copied verbatim from documents/BankOps_06_Database_Design.md so the two never drift silently.
[Migration(1)]
public class M0001_InitialSchema : Migration
{
    public override void Up()
    {
        Execute.Sql("CREATE SCHEMA IF NOT EXISTS platform;");
        Execute.Sql("CREATE SCHEMA IF NOT EXISTS audit;");
        Execute.Sql("CREATE SCHEMA IF NOT EXISTS catalog;");

        Execute.Sql(@"
            CREATE TABLE platform.app_settings (
              id uuid PRIMARY KEY DEFAULT uuidv7(),
              namespace text NOT NULL,
              setting_key text NOT NULL,
              value jsonb NOT NULL,
              version bigint NOT NULL DEFAULT 1,
              updated_at timestamptz NOT NULL DEFAULT now(),
              updated_by text NOT NULL,
              UNIQUE (namespace, setting_key)
            );
        ");

        Execute.Sql(@"
            CREATE TABLE audit.entries (
              id uuid PRIMARY KEY DEFAULT uuidv7(),
              occurred_at timestamptz NOT NULL DEFAULT now(),
              principal_ref text NOT NULL,
              action text NOT NULL,
              resource_type text NOT NULL,
              resource_id uuid,
              correlation_id text NOT NULL,
              safe_details jsonb NOT NULL DEFAULT '{}'::jsonb
            );
        ");
        // NOTE: NFR-SEC-06 requires audit to be append-only to app roles. That needs a DDL-owner
        // role distinct from the app runtime role (REVOKE against a table's own owner is a no-op
        // in Postgres). Local dev currently uses one role (bankops_dev) for both — this migration
        // does NOT yet enforce append-only at the DB level. Revisit before Phase 5/6 hardening;
        // see documents/PHASE1_CHECKLIST.md.

        Execute.Sql(@"
            CREATE TABLE catalog.services (
              id uuid PRIMARY KEY DEFAULT uuidv7(),
              code text NOT NULL UNIQUE,
              name text NOT NULL,
              owner_ref text,
              criticality text NOT NULL CHECK (criticality IN ('LOW','MEDIUM','HIGH','CRITICAL')),
              environment text NOT NULL,
              site text,
              graph_version bigint NOT NULL DEFAULT 1,
              created_at timestamptz NOT NULL DEFAULT now(),
              updated_at timestamptz NOT NULL DEFAULT now()
            );
        ");

        Execute.Sql(@"
            CREATE TABLE catalog.dependencies (
              id uuid PRIMARY KEY DEFAULT uuidv7(),
              service_id uuid NOT NULL REFERENCES catalog.services(id),
              target_kind text NOT NULL CHECK (target_kind IN ('SERVICE','ASSET','ENDPOINT','DATABASE')),
              target_id uuid NOT NULL,
              relation text NOT NULL CHECK (relation IN ('DEPENDS_ON','USES','CONNECTS_TO')),
              is_critical boolean NOT NULL DEFAULT true,
              published_version bigint NOT NULL,
              created_at timestamptz NOT NULL DEFAULT now(),
              UNIQUE (service_id, target_kind, target_id, relation, published_version)
            );
        ");

        Execute.Sql(@"
            CREATE INDEX dependencies_service_version_idx
              ON catalog.dependencies (service_id, published_version);
        ");
    }

    public override void Down()
    {
        Execute.Sql("DROP INDEX IF EXISTS catalog.dependencies_service_version_idx;");
        Execute.Sql("DROP TABLE IF EXISTS catalog.dependencies;");
        Execute.Sql("DROP TABLE IF EXISTS catalog.services;");
        Execute.Sql("DROP TABLE IF EXISTS audit.entries;");
        Execute.Sql("DROP TABLE IF EXISTS platform.app_settings;");
        Execute.Sql("DROP SCHEMA IF EXISTS catalog;");
        Execute.Sql("DROP SCHEMA IF EXISTS audit;");
        Execute.Sql("DROP SCHEMA IF EXISTS platform;");
    }
}
