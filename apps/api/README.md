# apps/api

Composition root, auth, versioned HTTP, SSE (see [BankOps_05_Backend_Design.md](../../documents/BankOps_05_Backend_Design.md)).

**Framework/version:** ASP.NET Core, .NET 10 (pinned 2026-09-24). Scaffolded with `dotnet new webapi --use-controllers` — controller-based, OpenAPI enabled by default. Template's sample `WeatherForecastController`/`WeatherForecast.cs` removed; nothing else added yet — no auth, no versioning, no module wiring. That's the next work, not scaffolding.

The API never waits on a live SolarWinds/T24/Commvault call to render a response — it reads a consistent materialized view/snapshot that the worker maintains. Every request authenticates, then checks operation permission + resource scope + feature availability + environment, at the API boundary and again inside the application command before commit.
