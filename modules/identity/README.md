# modules/identity

**Owns:** principal, capabilities, service scopes; OIDC session binding, RBAC enforcement.
**Phase:** 1 (Engineering foundation) — active now.
**Related requirements:** FR-001, NFR-SEC-01, NFR-SEC-02.

## Scope

- Verify bank-approved OIDC identity provider tokens (D-01: Bank AD/Entra via OIDC, MFA at identity provider — approved).
- Issue short-lived server sessions; secure HttpOnly/SameSite cookies; CSRF protection for cookie-authenticated writes.
- Own the permission/capability model consumed by every other module via `ICurrentPrincipal` — no other module re-implements auth.
- Deny by default. Every API entry authenticates then checks operation permission, resource scope, feature availability and environment.

## Structure

`Domain` / `Application` / `Infrastructure` / `Api` / `Contracts` / `Migrations` / `Tests` — standard module layout (see [BankOps_05_Backend_Design.md](../../documents/BankOps_05_Backend_Design.md)).

## Boundaries

- No other module queries identity's tables directly. Other modules depend only on the published `ICurrentPrincipal` / capability contract.
- Secrets (IdP client secret, signing keys) are vault references only — never in `platform.app_settings` or logs.

## Phase 1 exit evidence this module must satisfy

Unauthorized access denied in API (object-level, not just route-level); identity bound to every audited action.
