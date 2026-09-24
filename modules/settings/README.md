# modules/settings

**Owns:** branding revisions, feature flags, validated configuration (`platform.app_settings`).
**Phase:** 1 (Engineering foundation) — active now.
**Related requirements:** FR-003, FR-004, FR-006, NFR-UX-05.

## Scope

- Namespaced settings (organization / platform / module / connector / user) — editing one namespace must never overwrite another.
- Branding changes (logo, colors, favicon, language default, theme) apply without a frontend rebuild; invalid uploads rejected; every change is audited and revertible.
- Feature flags scoped by environment and role/cohort; a disabled flag must be enforced server-side, not just hidden in the UI.
- Settings `GET` never returns secrets — secret-bearing settings store a vault reference only.

## Structure

`BankOps.Modules.Settings.csproj` — `Contracts/` (DTOs), `Infrastructure/` (`SettingsRepository`, raw Npgsql against `platform.app_settings`), `Api/` (`SettingsController`). No `Domain/` yet — there's no invariant beyond "namespace+key is unique and versioned," nothing worth a domain layer for.

## What's real (2026-09-24)

`GET /api/v1/settings/{namespace}` (any authenticated principal), `PUT /api/v1/settings/{namespace}/{key}` (admin only). Optimistic concurrency via the `version` column — an update without a matching `expectedVersion` returns 409, not a silent overwrite (same pattern `BankOps_06_Database_Design.md` uses for incidents). Every successful write calls `modules/audit`'s published `IAuditWriter` — this module depends on audit's `Contracts` project only, never its `Infrastructure`/`Api`, enforcing the "modules depend on published contracts, not implementations" rule for real, not just in a comment. Verified end-to-end: create → 200, read → shows it, wrong version → 409, correct version → 200 v2, both writes appear in the audit trail. Automated in `tests/BankOps.Api.IntegrationTests/SettingsAndAuditTests.cs`.

## Boundaries

- Publishes settings/flags via a versioned read contract; other modules must not read `platform.app_settings` rows directly across a schema boundary — go through the published query.
- Any settings/permission cache (Redis) is invalidated immediately on publish — see NFR-PERF-06. **Not yet applicable** — no cache in front of this yet (Memurai/Valkey install is deferred, see `infra/README.md`).

## Still missing

Branding/logo/favicon upload+validation (FR-003's asset-handling half — this only covers the generic key/value read-write path), feature-flag storage here specifically (`apps/api`'s `FeatureFlags/` is config-backed for now, not yet reading from this table), settings namespace/key format validation (currently any string is accepted).

## Phase 1 exit evidence this module must satisfy

Deploy/rollback demonstrated for a settings change — **not done**, needs D-03 (hosting) resolved first. Migration up/down clean in a test DB — **done**, see `infra/DbMigrator` and `tests/BankOps.IntegrationTests`.
