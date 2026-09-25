# tests

Module, contract, failure and end-to-end tests — organized by what's being verified, not mirrored 1:1 with `modules/`.

## Categories

- **Module tests** — a single module's domain/application logic in isolation.
- **Contract tests** — a consumer against a published contract version, independent of the producer's internals.
- **Failure-injection tests** — e.g. "a connector crash degrades only its own module's freshness, other modules stay usable" (see the ATM change-safety example in [BankOps_01_Phased_Implementation_Plan.md](../documents/BankOps_01_Phased_Implementation_Plan.md)).
- **End-to-end / acceptance** — the critical acceptance journeys from [BankOps_02_Functional_Requirements.md](../documents/BankOps_02_Functional_Requirements.md) (outage → incident, source outage → stale, viewer denied mutation, etc).

## Phase 1 exit evidence to encode here first

Unauthorized access denied in API (object-level test, not just "route requires login"); migration up/down in a clean test DB; a deploy + rollback demonstrated.

## `BankOps.IntegrationTests` (2026-09-24)

Migration up/down against a genuinely disposable database — `DisposableDatabaseFixture.cs` creates a uniquely-named database, migrates it, tests run, then it's dropped. Locally that's still the same Postgres server as dev (no Docker on this dev machine — the fixture's own comments explain why); in CI (`.github/workflows/ci.yml`) it runs against a real disposable `postgres:18` service container instead, which is the actual isolation this exit-evidence item asks for.

## `BankOps.Api.IntegrationTests` (2026-09-24)

Automates the other Phase 1 exit-evidence item (object-level auth, previously manual curl) against the real OIDC/JWT validation pipeline, not a substituted auth handler. See its own README for a real `WebApplicationFactory` + minimal-hosting config-timing gotcha hit while building it. Also now covers `modules/settings`/`modules/audit` (`SettingsAndAuditTests.cs`) by reusing `DisposableDatabaseFixture` from `BankOps.IntegrationTests` — full create → version-conflict → update → audit-trail → RBAC sequence, against a real disposable database, real tokens, real API host. Runs sequentially, not in parallel (`xunit.runner.json`) — multiple test classes in this project mutate process-wide environment variables (`Oidc__Authority`, `Secrets__BankOpsDbConnectionString`) to work around the `WebApplicationFactory` config-timing gotcha, which isn't safe under xUnit's default parallel-by-class execution. `InteractiveLoginTests.cs` (Phase 2) drives the real authorization_code+PKCE round-trip by hand (redirects, cookies, code exchange) via `DevIdentityProviderFixture.GetAccessTokenViaLoginAsync` and confirms the resulting token works against apps/api the same as a client_credentials one.

## Catalog tests (2026-09-25)

`BankOps.Api.IntegrationTests/CatalogTests.cs` — `modules/catalog` end-to-end with the same real-DB/real-token/real-host setup: register → publish → read dependencies and dependents (including an edge moving off a service when it's re-published); publish rejections (cycle with its path, stale `expectedGraphVersion`, unknown target, unsupported target kind) leave the graph unchanged; field validation and duplicate code; audit entries for both write types; viewer reads but gets 403 on both writes; the capability manifest per role; the synthetic seed (idempotent, acyclic, served by the API). `ConcurrentPublishes_CannotTogetherCreateACycle` races A→B against B→A ten times — it fails when the repository's advisory lock is removed, so it genuinely guards that lock. `DependencyGraphTests.cs` unit-tests the cycle check on its own (no DB); disabling the check fails four tests across both files.

Run locally against any Postgres 18 (for `uuidv7()`) with the same variables CI sets, e.g. a throwaway container: `docker run -d -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:18`, then `POSTGRES_ADMIN_HOST=localhost POSTGRES_ADMIN_PORT=5432 POSTGRES_ADMIN_USER=postgres POSTGRES_ADMIN_PASSWORD=postgres dotnet test`.
