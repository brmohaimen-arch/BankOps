# modules/audit

**Owns:** append-only audit writer and export (`audit.entries`).
**Phase:** 1 (Engineering foundation) — active now.
**Related requirements:** NFR-SEC-06, NFR-OBS-05.

## Scope

- Record principal, action, resource type/ID, safe before/after summary, correlation ID and time for auth, config, policy, incident and action mutations.
- Append-only to application roles — no `UPDATE`/`DELETE` grant for app-level identities, only for a separately controlled retention/export job.
- Ship audit off-host; keep tamper evidence; time-sync across hosts.

## Structure

Split into two projects, deliberately: `Contracts/BankOps.Modules.Audit.Contracts.csproj` (just `IAuditWriter` + `AuditEntry`) and `BankOps.Modules.Audit.csproj` (`Infrastructure/PostgresAuditWriter`, `Api/AuditController`). Other modules (`modules/settings`) reference only the `Contracts` project — the split exists specifically so that's enforceable by the build, not just a comment saying "please don't reach into our internals."

## What's real (2026-09-24)

`IAuditWriter.WriteAsync` (Postgres-backed, called by `modules/settings` today) and `GET /api/v1/audit` (admin-only, filterable by `resourceType`/`resourceId`, bounded to 200 rows). Verified: `modules/settings` writes produce real audit rows with correct principal/action/correlationId; a viewer token gets 403 on the read endpoint. Automated in `tests/BankOps.Api.IntegrationTests/SettingsAndAuditTests.cs`.

## Boundaries

- Every other module writes audit entries through the published `IAuditWriter` contract — never a direct insert into `audit.entries` from outside this module.
- Audit is read by entitled roles only (data governance: audit is its own classification tier) — enforced today via `[Authorize(Roles = "admin")]`.

## Known gap — NOT actually append-only at the DB level yet

`bankops_dev` both owns the `audit.entries` table and is the app's runtime role — `REVOKE UPDATE, DELETE` against a table's own owner is a no-op in Postgres, so nothing currently stops the app role from editing/deleting audit rows at the DB layer. Fixing this needs a DDL-owner role distinct from the runtime role (see `infra/DbMigrator/README.md`). "Off-host shipping" is also not implemented — audit writes don't yet flow anywhere but the DB table; D-04 (real telemetry backend) is still open, and this is the same swappable-later pattern used for telemetry exporters elsewhere.

## Phase 1 exit evidence this module must satisfy

Attempted edit of an audit row is denied at the DB role level — **not done**, see gap above. Off-host shipping and a restore/read-back — **not done**, blocked on D-04.
