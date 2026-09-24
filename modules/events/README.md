# modules/events

**Owns:** normalized event metadata, intake dedup, outbox, dead letters (`events` schema).
**Phase:** 3 (Checks and ingestion) — not built yet; folder reserved to keep schema-ownership planning honest.
**Related requirements:** FR-104, NFR-REL-05.

## Scope (when Phase 3 starts)

- Normalize incoming signals to a versioned event envelope; accept a duplicate `(sourceId, sourceEventId)` exactly once.
- Malformed events go to a bounded dead-letter store, never silently dropped.
- Transactional outbox for any event emitted alongside a DB change; consumers are idempotent (at-least-once delivery).

## Boundaries

No other module writes to `events.outbox` directly — publish through this module's contract so dedup/outbox guarantees hold.
