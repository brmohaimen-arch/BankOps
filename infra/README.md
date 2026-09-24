# infra

Deploy, DB migrations, telemetry configuration.

## Phase 1 scope

- CI: build/lint/contract/security scan pipeline; pin dependencies; generate an SBOM (phased-plan backlog step 4).
- A disposable integration test environment, separate from dev/test/production — network separation is preserved (phased-plan delivery rules).
- Migration pipeline for PostgreSQL, wired to run up/down cleanly in a clean test DB — this is Phase 1 exit evidence.
- Environment config for OIDC (D-01: Bank AD/Entra), least-privilege DB roles per module, secret references (vault, not literal values).

## Not yet decided (Phase 0, still open)

Hosting/network zones/HA-DR targets (D-03), existing telemetry/queue reuse (D-04) — this folder should not encode a specific bank hosting target until those are confirmed. Use a local/dev-only telemetry stack in the meantime.

## Cache layer: dev vs. production divergence (deliberate)

- **Local dev:** Memurai Developer (Windows-native, Redis-protocol-compatible, proprietary-but-free). Chosen only because Valkey has no native Windows build and Docker Desktop isn't set up on the current dev machine.
- **Production target:** Valkey (BSD-3-clause, Linux Foundation fork), run as an official `valkey/valkey` container. Chosen over Redis itself because Valkey stayed permissively licensed through the 2024 Redis relicensing, while Redis is now AGPLv3/RSALv2/SSPLv1 tri-licensed — Valkey is the cleaner answer for a bank's legal review.
- **Why this is safe:** both speak the Redis wire protocol, and [BankOps_05_Backend_Design.md](../documents/BankOps_05_Backend_Design.md) already requires app code to depend only on cache abstractions (short TTL + event invalidation, bounded DB fallback on cache failure) — never a vendor-specific client feature. Swapping the backing server at deploy time should require zero application code changes.
- **Action item before Phase 5/6:** confirm this swap actually works (integration test against both Memurai in dev and a Valkey container in CI) before relying on it as a genuine "just swap the backend" claim.
