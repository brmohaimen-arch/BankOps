# modules/incidents

**Owns:** incident lifecycle, timeline, evidence links, assignments (`incidents` schema).
**Phase:** 4 (Incident MVP) — not built yet; folder reserved to keep schema-ownership planning honest.
**Related requirements:** FR-201–FR-209.

## Scope (when Phase 4 starts)

- Group repeated/causally linked alerts using explicit rules + the catalog dependency graph; preserve raw evidence.
- Lifecycle Open → Acknowledged → Investigating → Mitigating → Resolved → Closed (+ Reopened); invalid transitions rejected.
- Unique human incident number (sequence, never reused, never a foreign key) plus immutable UUIDv7 ID.
- `rootCauseCandidate` is always a hypothesis with rule ID and evidence links — never a silent assertion; label Undetermined when evidence is insufficient.

## Boundaries

`incidents.service_id` has no cross-schema FK to `catalog` by design (contract-enforced, not SQL-enforced) — see [BankOps_06_Database_Design.md](../../documents/BankOps_06_Database_Design.md).
