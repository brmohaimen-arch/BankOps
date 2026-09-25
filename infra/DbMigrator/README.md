# infra/DbMigrator

FluentMigrator-based migration runner for the `bankops_dev` PostgreSQL database (local dev). Not a deployable service — a CLI tool.

## Usage

Requires a `.env` at the repo root with `BANKOPS_DB_HOST`, `BANKOPS_DB_PORT`, `BANKOPS_DB_NAME`, `BANKOPS_DB_USER`, `BANKOPS_DB_PASSWORD` (see repo root `.gitignore` — that file is never committed).

```bash
dotnet run -- up      # apply all pending migrations
dotnet run -- down    # roll back everything this runner knows about (MigrateDown(0))
dotnet run -- seed    # migrate up, then load synthetic catalog data (dev only)
```

## Synthetic seed data (2026-09-25)

`Seeds/CatalogSyntheticData.cs` — 14 services modelled on a typical bank estate (core banking, payments hub, card switch, SWIFT gateway, HSMs, digital channels…) and 17 dependency edges between them. Every `site` is "Synthetic DC-1/2" so seeded rows can't be mistaken for a real inventory in a screenshot. Rules:

- Only runs via the explicit `seed` command — never part of `up`.
- Only seeds an **empty** catalog, so it never mixes with hand-entered data; a second run is a no-op.
- Writes the same versioning shape `modules/catalog`'s publish endpoint produces (dependency sets at `graph_version` 2).
- Bypasses the API, so it is **not audited** — acceptable only because it's disposable dev data.
- `CatalogTests.SyntheticSeed_IsIdempotentAcyclicAndServedByTheApi` seeds a disposable database, reads everything back through the API, and re-checks the graph with the same cycle check the publish endpoint uses.

## Scope (Phase 1)

`Migrations/M0001_InitialSchema.cs` creates exactly what Phase 1 needs: `platform.app_settings`, `audit.entries`, `catalog.services`, `catalog.dependencies` + the one index the database design doc specifies for it. DDL is copied verbatim from [documents/BankOps_06_Database_Design.md](../../documents/BankOps_06_Database_Design.md) so the two don't silently drift. `monitoring`, `events`, `incidents`, `knowledge` schemas are Phase 3–4 — deliberately not created yet.

## Known simplification — read before adding a Phase 2+ migration

The backend design says **every module owns its own migrations** (`modules/<name>/Migrations`). This tool instead runs one shared FluentMigrator assembly for all of Phase 1's schemas. That was a deliberate shortcut to get a working up/down pipeline fast, not the target architecture — revisit before the next migration by either (a) giving each module its own migration assembly/runner, or (b) keeping one runner but organizing migration classes so ownership is still enforced by code review (a `catalog` migration must not touch `audit` tables, etc.), consistent with the module-boundary rules elsewhere in the docs.

**Status (2026-09-25):** still undecided, and still only `M0001`. `modules/catalog`'s Phase 2 endpoints needed no schema change (Phase 1's `catalog.services`/`catalog.dependencies` already carry the versioning model), so nothing forced the choice yet. Whichever Phase 2+ change first needs a migration has to settle it.

## Known gap — audit append-only is NOT yet enforced at the DB level

NFR-SEC-06 requires audit to be append-only to application roles. Enforcing that properly needs a DDL-owner role distinct from the app's runtime role — `REVOKE UPDATE, DELETE` against a table's *own owner* is a no-op in Postgres, ownership bypasses ACLs. Local dev currently uses a single `bankops_dev` role for both schema ownership and app runtime, so this migration does not yet actually block updates/deletes on `audit.entries`. Revisit before Phase 5/6 hardening.
