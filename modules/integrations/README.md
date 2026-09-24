# modules/integrations

**Owns:** connector registry and per-vendor adapters.
**Phase:** 3 (mock/lab connectors) — not built yet; production connectors are Phase 5, gated by D-02 (approved source/data classification, currently Open).
**Related requirements:** FR-104, FR-107, NFR-SEC-05.

## Scope (when Phase 3 starts)

- Connector modes `MOCK` / `LAB` / `PRODUCTION`; switching to `PRODUCTION` requires admin rights and an approved network/source scope (D-02, D-03).
- Each connector instance: own schedule, token bucket, concurrency cap, timeout, retry budget, circuit breaker, cursor, health state.
- Credentials are vault references injected at runtime — never stored in `platform.app_settings` or returned by any GET.
- On failure: preserve last successful observation with age, mark source stale/unknown at a configured cutoff; never mark the target healthy from a dead connector.

## Boundaries

Vendor-specific payload mapping stays inside that vendor's adapter — it never leaks into `monitoring` or `incidents` domain models. `D-07` ("Dogo") is explicitly excluded from scope here until its product identity is confirmed.
