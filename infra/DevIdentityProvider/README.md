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

## Interactive login (added 2026-09-24, for apps/web)

Real `authorization_code` + PKCE flow, for apps/web's actual browser login — `client_credentials` alone can't authenticate a human. `AuthorizationController.cs` adds `/connect/authorize`, a plain-HTML `/login` form (`DevUserStore.cs`), and `/logout`. Login uses a separate cookie scheme (`AddCookie`) from the tokens OpenIddict itself issues — "are you signed in right now" and "here is a bearer token proving who you are" are different concerns even in one dev tool.

| Dev user | Password | Role |
|---|---|---|
| `admin@bankops.dev` | `admin123` | `admin` |
| `viewer@bankops.dev` | `viewer123` | `viewer` |

Public SPA client (no secret — PKCE substitutes for one): `bankops-web`, redirect URI `http://localhost:5173/callback` (Vite's default dev port — update here if apps/web pins a different one).

A real gotcha hit building this: the cookie challenge's `RedirectUri` must be a **relative** path/query, not `Request.GetEncodedUrl()`'s absolute URL — `LocalRedirect` (used once login actually succeeds) rejects absolute URLs even for the same host by design, to close off open-redirect attacks. Passing an absolute URL through crashed with `InvalidOperationException: The supplied URL is not local`.

Full round-trip verified manually with curl (authorize → login redirect → login POST → authorize again → code → token exchange with the matching `code_verifier` → decoded JWT has the right `sub`/`role`/`name`) and automated in `tests/BankOps.Api.IntegrationTests/InteractiveLoginTests.cs`.

## Known simplifications (dev-only, do not carry forward)

- **Access tokens are plain signed JWTs** (`DisableAccessTokenEncryption()`), not the encrypted JWE OpenIddict issues by default — chosen so `apps/api` can validate with plain `AddJwtBearer()` against the JWKS endpoint, closer to what Entra will actually hand it.
- **Ephemeral dev signing/encryption certs**, regenerated every restart (`AddDevelopmentSigningCertificate()`/`AddDevelopmentEncryptionCertificate()`).
- **Plain HTTP**, no TLS (`DisableTransportSecurityRequirement()`) — fine for localhost, never for anything real.
- **Plaintext dev-user passwords** in `DevUserStore.cs` — fine only because this store is unreachable outside localhost and rebuilt from scratch every restart. Never a pattern to reuse anywhere real users/passwords matter.
- No consent screen — every registered client is a trusted first-party dev tool. D-01's real Entra integration is a completely different, bank-managed trust model; this doesn't attempt to simulate it.

## Verified against apps/api (2026-09-24)

Four-case object-level RBAC test (client_credentials), all passing: no token → 401, valid token + no role requirement → 200, valid token + wrong role on a role-gated endpoint → 403, valid token + correct role → 200. See `apps/api/Controllers/AuthProbeController.cs` (temporary — delete once `modules/identity` has real endpoints). Interactive-login-issued tokens verified against the same endpoints too — same signing key, same claim shape, apps/api doesn't need to know which grant produced the token.
