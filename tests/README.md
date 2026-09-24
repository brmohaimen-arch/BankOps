# tests

Module, contract, failure and end-to-end tests — organized by what's being verified, not mirrored 1:1 with `modules/`.

## Categories

- **Module tests** — a single module's domain/application logic in isolation.
- **Contract tests** — a consumer against a published contract version, independent of the producer's internals.
- **Failure-injection tests** — e.g. "a connector crash degrades only its own module's freshness, other modules stay usable" (see the ATM change-safety example in [BankOps_01_Phased_Implementation_Plan.md](../documents/BankOps_01_Phased_Implementation_Plan.md)).
- **End-to-end / acceptance** — the critical acceptance journeys from [BankOps_02_Functional_Requirements.md](../documents/BankOps_02_Functional_Requirements.md) (outage → incident, source outage → stale, viewer denied mutation, etc).

## Phase 1 exit evidence to encode here first

Unauthorized access denied in API (object-level test, not just "route requires login"); migration up/down in a clean test DB; a deploy + rollback demonstrated.
