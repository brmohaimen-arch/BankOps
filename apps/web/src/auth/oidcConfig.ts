import type { AuthProviderProps } from "react-oidc-context";

// infra/DevIdentityProvider — not D-01's real IdP. Update authority/client_id here when the real
// Entra integration happens; nothing else in the app should need to change (react-oidc-context
// abstracts the token exchange either way).
export const oidcConfig: AuthProviderProps = {
  authority: "http://localhost:5026",
  client_id: "bankops-web",
  redirect_uri: "http://localhost:5173/callback",
  post_logout_redirect_uri: "http://localhost:5173/",
  scope: "bankops.api openid profile",
  response_type: "code",
  // The dev IdP doesn't support silent (iframe) renewal — avoid noisy background failures for a
  // feature that isn't there yet. Revisit once D-01's real IdP is wired up.
  automaticSilentRenew: false,
};
