# modules/monitoring

**Owns:** checks, observations, latest health snapshots, source freshness (`monitoring` schema).
**Phase:** 3 (Checks and ingestion) — not built yet; folder reserved to keep schema-ownership planning honest.
**Related requirements:** FR-103, FR-104, FR-105, FR-106.

## Scope (when Phase 3 starts)

- Scheduled HTTP(S)/TCP/DNS checks from named probe zones; separately track target health vs. collector/connector health.
- Collector loss → Unknown/Stale after a configured deadline. Never report Healthy purely from absent data.
- Compute business-service state from catalog dependencies + defined criticality rules.

## Boundaries

`catalog` owns the dependency graph; `monitoring` only reads it through the published query interface. Do not start implementation here before Phase 2 (catalog) is stable.
