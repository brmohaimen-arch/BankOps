# modules/audit

**Owns:** append-only audit writer and export (`audit.entries`).
**Phase:** 1 (Engineering foundation) — active now.
**Related requirements:** NFR-SEC-06, NFR-OBS-05.

## Scope

- Record principal, action, resource type/ID, safe before/after summary, correlation ID and time for auth, config, policy, incident and action mutations.
- Append-only to application roles — no `UPDATE`/`DELETE` grant for app-level identities, only for a separately controlled retention/export job.
- Ship audit off-host; keep tamper evidence; time-sync across hosts.

## Structure

`Domain` / `Application` / `Infrastructure` / `Api` / `Contracts` / `Migrations` / `Tests`.

## Boundaries

- Every other module writes audit entries through a published `IAuditWriter` contract — never a direct insert into `audit.entries` from outside this module.
- Audit is read by entitled roles only (data governance: audit is its own classification tier).

## Phase 1 exit evidence this module must satisfy

Attempted edit of an audit row is denied at the DB role level; off-host shipping and a restore/read-back are demonstrated.
