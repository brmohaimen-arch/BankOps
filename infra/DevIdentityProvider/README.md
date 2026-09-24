# infra/DevIdentityProvider

A throwaway OpenIddict-based OIDC issuer for local dev. **Not D-01's actual identity provider** — D-01 already names Bank AD/Entra via OIDC as the real one. This exists only so `apps/api` can be built and tested against a genuine OIDC token contract (real discovery document, real JWKS, real signed JWTs) before that real integration happens in a later phase.

## Why OpenIddict

Apache 2.0 licensed, runs in-process (`dotnet run`), no Docker needed — Docker Desktop isn't installed on this dev machine (same constraint noted in `infra/README.md` for the Valkey/cache decision). Duende IdentityServer was considered and rejected for this purpose: its licensing terms are a question worth avoiding entirely for a bank project, even for a dev-only tool nobody ships.

## Running it

```bash
dotnet run --launch-profile http   # listens on http://localhost:5026
```

Entirely in-memory — client registrations reset on every restart (see `Program.cs`, which seeds two dev clients on startup).

## Dev clients (client_credentials grant only)

| client_id | client_secret | role claim issued |
|---|---|---|
| `bankops-dev-admin` | `dev-admin-secret` | `admin` |
| `bankops-dev-viewer` | `dev-viewer-secret` | `viewer` |

Which client_id you authenticate as decides your role — this is a dev shortcut, not how real role/permission resolution will work. That belongs to `modules/identity` once it exists.

```bash
curl -X POST http://localhost:5026/connect/token \
  -d "grant_type=client_credentials&client_id=bankops-dev-admin&client_secret=dev-admin-secret&scope=bankops.api"
```

## Known simplifications (dev-only, do not carry forward)

- **Access tokens are plain signed JWTs** (`DisableAccessTokenEncryption()`), not the encrypted JWE OpenIddict issues by default — chosen so `apps/api` can validate with plain `AddJwtBearer()` against the JWKS endpoint, closer to what Entra will actually hand it.
- **Ephemeral dev signing/encryption certs**, regenerated every restart (`AddDevelopmentSigningCertificate()`/`AddDevelopmentEncryptionCertificate()`).
- **Plain HTTP**, no TLS (`DisableTransportSecurityRequirement()`) — fine for localhost, never for anything real.
- Only `client_credentials` grant is wired up — no interactive login page. D-01's real flow (Entra, user-interactive) will need `authorization_code` + PKCE, which this tool intentionally does not attempt to simulate.

## Verified against apps/api (2026-09-24)

Four-case object-level RBAC test, all passing: no token → 401, valid token + no role requirement → 200, valid token + wrong role on a role-gated endpoint → 403, valid token + correct role → 200. See `apps/api/Controllers/AuthProbeController.cs` (temporary — delete once `modules/identity` has real endpoints).
