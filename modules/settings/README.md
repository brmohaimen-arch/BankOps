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

`Domain` / `Application` / `Infrastructure` / `Api` / `Contracts` / `Migrations` / `Tests`.

## Boundaries

- Publishes settings/flags via a versioned read contract; other modules must not read `platform.app_settings` rows directly across a schema boundary — go through the published query.
- Any settings/permission cache (Redis) is invalidated immediately on publish — see NFR-PERF-06.

## Phase 1 exit evidence this module must satisfy

Deploy/rollback demonstrated for a settings change; migration up/down clean in a test DB.
