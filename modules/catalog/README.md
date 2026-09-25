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

## Phase 1 scope

Schema and migrations (`M0001_InitialSchema`) — no endpoints.

## What's real (Phase 2, 2026-09-25)

`BankOps.Modules.Catalog.csproj` — `Contracts/` (its own project, like `modules/audit`: `ICatalogQuery` + DTOs, the published read interface for monitoring/incidents), `Domain/` (`CatalogRules` field rules, `DependencyGraph` cycle check — pure, unit-tested), `Infrastructure/` (`CatalogRepository`, raw Npgsql), `Api/` (`CatalogController`). No schema change was needed — Phase 1's tables already carry everything below.

| Endpoint | Who | Notes |
|---|---|---|
| `GET /api/v1/catalog/services` | any authenticated | Ordered CRITICAL → LOW, then code. `limit` clamped to 1–500 (NFR-SEC-07). |
| `GET /api/v1/catalog/services/{id}` | any authenticated | Service + its dependencies at the current published version + its dependents (other services whose *current* set points at it). |
| `POST /api/v1/catalog/services` | admin | FR-101. 400 with per-field `errors`; 409 `SERVICE_CODE_TAKEN`. Audited `catalog.service.create`. |
| `PUT /api/v1/catalog/services/{id}/dependencies` | admin | FR-102 publish: replaces the whole outgoing set as `graph_version + 1`. Requires `expectedGraphVersion` (409 `GRAPH_VERSION_CONFLICT` otherwise — no blind overwrites, same rule as settings). 422 `DEPENDENCY_CYCLE` (with the cycle path by code), `DEPENDENCY_TARGET_NOT_FOUND`, `TARGET_KIND_NOT_SUPPORTED_YET`. Audited `catalog.dependencies.publish`. |

**Versioning model:** `services.graph_version` is the service's current published set; `dependencies` rows are immutable per `published_version`, so earlier sets stay as history. A new service starts at version 1 with no rows ("published, empty").

**Concurrency:** every publish takes one graph-wide `pg_advisory_xact_lock` before its cycle check. Without it, A→B and B→A published at the same moment each pass their own check and together form a cycle — `CatalogTests.ConcurrentPublishes_CannotTogetherCreateACycle` reproduces exactly that when the lock is removed (verified 2026-09-25). Publishes are rare admin actions, so serializing them is free.

**Health is not here, on purpose.** Catalog owns what exists and how it connects; whether it's up is monitoring's evidence (Phase 3). apps/web shows every service as **Unknown** with the reason, never a guessed status.

**Synthetic data:** `infra/DbMigrator` → `dotnet run -- seed` (14 services, 17 edges, every site labelled "Synthetic"). See its README.

## Still missing

- **Only `SERVICE` targets can be published.** `ASSET`/`ENDPOINT`/`DATABASE` are rejected with 422 until their registries exist — the README requires targets be validated at publish, and there's nothing to validate those ids against yet.
- **No draft/review step** — publish is direct (admin + version check + cycle check). Draft/review needs an approvals concept that doesn't exist yet.
- **No edit/retire for a service's own fields** (name, owner, criticality…) — register and publish only.
- **No CIDR allowlist validation** — belongs with endpoint/host registration, which isn't built.
- **No dependency editing UI** — the publish endpoint is API-only for now; apps/web shows the graph read-only.
