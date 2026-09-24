# modules/notifications

**Owns:** templates, delivery, escalation.
**Phase:** 4 (Incident MVP) — not built yet.
**Related requirements:** FR-207, FR-209.

## Scope (when Phase 4 starts)

- Notify eligible teams on important state changes with dedup, quiet hours, escalation policy and delivery status.
- Idempotent dispatch — an event replay must never send a duplicate notification.
- Maintenance windows suppress paging but never suppress underlying evidence/audit.
