# modules/runbooks

**Owns:** reviewed operational knowledge (`knowledge` schema).
**Phase:** 4 (Incident MVP) — not built yet.
**Related requirements:** FR-301.

## Scope (when Phase 4 starts)

- Manage reviewed runbooks: owner, symptoms, diagnosis, risk class, safe steps, verification, rollback.
- Published content is immutable — a change creates a new revision, never an overwrite.
- Unpublished drafts are invisible to operators; incidents link only to a published version.
