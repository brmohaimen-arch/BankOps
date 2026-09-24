# modules/automation

**Owns:** approved action workflows (later release).
**Phase:** 7 (Controlled action pilot) — explicitly excluded from MVP. Folder exists only so the module boundary is visible in the repo shape from day one; do not implement anything here before Phase 7 has its own separate approval.
**Related requirements:** FR-302–FR-304.

## Scope (when Phase 7 starts, separately approved)

- Allowlisted actions only — arbitrary shell/command execution is never reachable through the normal API.
- Maker-checker: the requester cannot approve their own high-risk production request; an expired approval fails closed.
- Execution status, output reference, actor and post-action health check are all captured; UI shows Running until independently verified — failure is never shown as Success.
