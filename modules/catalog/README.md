# modules/catalog

**Owns:** services, assets, dependency edges (`catalog` schema).
**Phase:** schema/ownership established in Phase 1 (per phased-plan backlog step 6: "establish schema ownership before cross-module features"); full UI/dependency-graph features land in Phase 2 (Catalog and experience shell).
**Related requirements:** FR-101, FR-102, FR-106.

## Scope

- Register services, applications, hosts, endpoints, databases and relationships with owner, site, environment, criticality.
- Draft/review/publish versioning for the dependency graph; reject or flag cyclic/invalid dependencies at publish.
- New host/endpoint registration is validated against an approved network/CIDR allowlist before it can be attached to any check (closes the SSRF-style gap raised for admin-added monitoring targets — see FR-101/FR-103 acceptance conditions).

## Structure

`Domain` / `Application` / `Infrastructure` / `Api` / `Contracts` / `Migrations` / `Tests`.

## Boundaries

- `catalog.dependencies.target_id` is a polymorphic reference validated by the publish workflow, not a SQL FK (see [BankOps_06_Database_Design.md](../../documents/BankOps_06_Database_Design.md)).
- Other modules (monitoring, incidents) read catalog data only through a published query interface, never a cross-schema join.

## Phase 1 scope (this phase only)

Schema, migrations, and the published read contract exist and are stable enough for `identity`/`settings`/`audit` to depend on nothing here yet. No dependency-graph UI, no publish workflow required until Phase 2.
