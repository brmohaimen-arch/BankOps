# contracts

Versioned DTOs and event schemas shared across modules — the *only* thing modules are allowed to depend on across a module boundary (never another module's tables or internal types).

## Rules

- Every contract carries a `schemaVersion`. A breaking change is a new version, not a mutation of the old one — see NFR-UX-04 ("published contracts support at least one compatible rolling release").
- Contract tests live in `tests/`, not inside the owning module's own test suite, so a consumer can verify compatibility independently.
- Example payloads are checked in alongside each contract so adapter/consumer tests have fixtures to run against.

## Representative contracts (see [BankOps_05_Backend_Design.md](../documents/BankOps_05_Backend_Design.md) for the full table)

`ISignalIngestor.Accept(v1)`, `IServiceHealthQuery.Get`, `IIncidentCommand.Acknowledge`, `IConnector.Collect`, `IModuleManifest`. Events: `ObservationAccepted.v1`, `SourceFreshnessChanged.v1`, `ServiceHealthChanged.v1`, `IncidentOpened.v1`, `IncidentStatusChanged.v1`.

## `BankOps.Contracts.csproj` (2026-09-24)

The first real code here — not a DTO/event yet, but a **platform primitive** in the same sense `BankOps_05_Backend_Design.md` uses the term (`IClock`, `IIdGenerator`, `ICurrentPrincipal`, ...): `ICurrentPrincipal`, implemented by `apps/api` (the only layer that knows about `HttpContext`) and consumed by `modules/settings`/`modules/audit` to know who's making a request. No versioned event DTOs exist yet — those show up once `modules/events`/`modules/incidents` are built (Phase 3–4).
