# tests/BankOps.Api.IntegrationTests

Automates the object-level RBAC check that was previously run by hand with curl (see `documents/PHASE1_CHECKLIST.md`, 2026-09-24): no token → 401, valid token → 200, valid token wrong role → 403, valid token correct role → 200.

## Why this runs a real subprocess, not a fake auth handler

`DevIdentityProviderFixture` starts `infra/DevIdentityProvider` as a real `dotnet run` subprocess on a real port (5099, distinct from the manual-use port 5026) and waits for its discovery endpoint to respond. `AuthTests` then hosts `apps/api` via `WebApplicationFactory<Program>` pointed at that real issuer, and exercises the actual `AddJwtBearer` validation pipeline end-to-end with a real signed JWT.

This is deliberate. The bug this test suite exists to catch — `MapInboundClaims` silently rewriting role claims and breaking every `[Authorize(Roles = ...)]` check — lives specifically in that real HTTP token-validation path. A substituted `TestAuthHandler` (the more common/faster pattern for testing `[Authorize]` in isolation) would test *around* that bug, not *for* it.

## A real gotcha hit building this

`WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(...))` does **not** reliably override configuration that `apps/api`'s `Program.cs` reads synchronously via `builder.Configuration[...]` before the host finishes building — the override wasn't visible yet at that point, so the test silently validated against the wrong (default fallback) issuer and every token-bearing test failed with 401. Fixed by setting `Oidc__Authority` as a real environment variable before constructing the factory instead — `WebApplication.CreateBuilder(args)` reads environment variables as one of its own default sources, which sidesteps the config-overlay timing question entirely. See `AuthTests.InitializeAsync()`.
