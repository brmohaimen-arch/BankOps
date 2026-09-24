# apps/web

UI, components and route manifests. Consumes versioned API contracts only — never interprets raw vendor/connector payloads directly (see [BankOps_04_Frontend_Design.md](../../documents/BankOps_04_Frontend_Design.md)).

**Framework/version:** React 19 + TypeScript via Vite (pinned 2026-09-24). **Component library: Ant Design** — explicitly chosen over MUI/custom-Tailwind for built-in RTL support and enterprise-dashboard-shaped components; this was an open Phase 0 decision ("bank-approved component library"), resolved for the lab build.

Navigation is generated from the reviewed module manifest + the server-supplied capability list (`/api/v1/me/capabilities`, see `src/api/useCapabilities.ts`). Hiding a menu item is presentation only — verified: a viewer can still reach `/admin/appearance` directly by URL (its `GET` is intentionally open to any authenticated role) but a `PUT` from that page returns 403 regardless of what the UI shows, same pattern as every other Phase 1/2 endpoint.

## Auth (2026-09-24)

`react-oidc-context` + PKCE against `infra/DevIdentityProvider`, config in `src/auth/oidcConfig.ts`. `RequireAuth` in `App.tsx` triggers `signinRedirect()` for unauthenticated users; `pages/CallbackPage.tsx` waits for `react-oidc-context` to finish processing the code exchange, then hands off to the router.

**A real bug found only by testing in an actual browser, not curl:** the unauthenticated `/connect/authorize` challenge came back as HTTP 401 with a `Location` header instead of a 302. curl-based manual testing during Phase 1/2 backend work didn't catch this — curl doesn't auto-follow either status, so I was reading the `Location` header myself either way. A real browser's top-level navigation only follows 3xx, so the login page silently never opened. Fixed in `infra/DevIdentityProvider/Controllers/AuthorizationController.cs` (`Redirect()` instead of `Challenge()`) — see that file's comments. **Lesson applied:** once there's a real frontend, verify auth flows in the actual browser pane, not just against curl/automated HTTP clients.

**Sign-out also needed real fixes**, found the same way: `signoutRedirect()` alone didn't end the IdP's session (OpenIddict had no end-session endpoint registered at all — `SetEndSessionEndpointUris` was missing), then once added, OpenIddict rejected the request twice more (`post_logout_redirect_uri` invalid because `client_id` wasn't in the request — oidc-client-ts doesn't send it by default, added via `extraQueryParams`; then `unauthorized_client` because the `bankops-web` client registration was missing the `Endpoints.EndSession` permission). All three now fixed and verified: sign-out actually ends the session, confirmed by landing on the real login form afterward instead of being silently re-authenticated.

## RTL / i18n (2026-09-24)

`react-i18next` (`src/i18n/`, `en.json`/`ar.json`) + Ant Design's `ConfigProvider direction="rtl"`. Verified in-browser: full layout flip (sidebar moves to the right, header order reverses, text right-aligns) on language switch, matching the frontend design doc's explicit "left sidebar becomes right sidebar in RTL." Almarai font (confirmed SIL OFL 1.1, free for commercial use — D-06) loaded via Google Fonts in `index.html`.

## Pages (2026-09-24)

`AppearancePage` and `AuditPage` are real, working against `modules/settings`/`modules/audit`'s Phase 1 endpoints — not placeholders. Verified end-to-end in-browser: color change saves without a rebuild, shows up immediately in the audit log with the real logged-in user's identity (not a service-account artifact from earlier client_credentials testing). `DashboardPage`/`CatalogPage` are honest empty states — `modules/catalog` doesn't have real endpoints yet (next item on `documents/PHASE2_CHECKLIST.md`), and faking mock data there would contradict the design's own "never dress up missing data as a status" principle.
