# apps/api

Composition root, auth, versioned HTTP, SSE (see [BankOps_05_Backend_Design.md](../../documents/BankOps_05_Backend_Design.md)).

**Framework/version:** ASP.NET Core, .NET 10 (pinned 2026-09-24). Scaffolded with `dotnet new webapi --use-controllers` — controller-based, OpenAPI enabled by default. Template's sample `WeatherForecastController`/`WeatherForecast.cs` removed.

**Auth (2026-09-24):** JWT bearer auth wired up against [infra/DevIdentityProvider](../../infra/DevIdentityProvider/README.md) (dev-only OIDC issuer — not D-01's real IdP). `MapInboundClaims = false` is required in `Program.cs` — without it, role claims get silently rewritten to legacy WS-Identity URIs and every `[Authorize(Roles = ...)]` check fails closed with a 403 that looks like a config problem, not a bug. `Controllers/AuthProbeController.cs` is a temporary object-level RBAC test endpoint (401/403/200 cases verified) — delete it once `modules/identity` has real endpoints. Still open: no versioning scheme, no module wiring beyond this one probe controller.

The API never waits on a live SolarWinds/T24/Commvault call to render a response — it reads a consistent materialized view/snapshot that the worker maintains. Every request authenticates, then checks operation permission + resource scope + feature availability + environment, at the API boundary and again inside the application command before commit.
