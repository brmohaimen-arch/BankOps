# apps/worker

Check runners, connector jobs, outbox dispatch (see [BankOps_05_Backend_Design.md](../../documents/BankOps_05_Backend_Design.md)).

**Framework/version:** not yet pinned — same runtime as `apps/api`, decision still open in Phase 0.

Runs under a dedicated service identity (never the interactive-user identity). Long-running checks, connector collection and notification dispatch all happen here, on bounded schedules/webhooks — never inline in an API request.
