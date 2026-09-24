# apps/web

UI, components and route manifests. Consumes versioned API contracts only — never interprets raw vendor/connector payloads directly (see [BankOps_04_Frontend_Design.md](../../documents/BankOps_04_Frontend_Design.md)).

**Framework/version:** not yet pinned — proposed React + TypeScript, exact supported versions are a Phase 0 decision still open. Do not `npm create`/initialize a real project here until that's confirmed; this folder currently only reserves the module's place in the repo shape.

Navigation is generated from the reviewed module manifest + the server-supplied capability list (`/api/v1/me/capabilities`). Hiding a menu item is presentation only — the API is independently authorized regardless of what the UI shows.
