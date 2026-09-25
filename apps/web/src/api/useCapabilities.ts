import { useEffect, useState } from "react";
import { useAuth } from "react-oidc-context";
import { useOutletContext } from "react-router-dom";
import { apiFetch } from "./client";

export interface ModuleManifestEntry {
  id: string;
  label: string;
  route: string;
  requiresRole: string | null;
}

export interface Capabilities {
  user: { subject: string; roles: string[] };
  modules: ModuleManifestEntry[];
}

interface CapabilitiesResult {
  token: string;
  capabilities: Capabilities | null;
  error: unknown;
}

// FR-002/frontend design: "Navigation is generated from a reviewed module manifest and an
// authorized server-supplied capability list." Hiding a menu item here is presentation only —
// apps/api independently rejects any route this doesn't list, same as every other endpoint.
export function useCapabilities() {
  const auth = useAuth();
  const token = auth.isAuthenticated ? auth.user?.access_token : undefined;
  // Each result remembers which token it was fetched with, so "loading" is derived (no result yet
  // for the current token) rather than set synchronously inside the effect.
  const [result, setResult] = useState<CapabilitiesResult | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }

    let cancelled = false;
    apiFetch("/api/v1/me/capabilities", token)
      .then((response) => response.json())
      .then((data: Capabilities) => {
        if (!cancelled) {
          setResult({ token, capabilities: data, error: null });
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setResult({ token, capabilities: null, error: err });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [token]);

  const current = result?.token === token ? result : null;
  return {
    capabilities: current?.capabilities ?? null,
    loading: current === null,
    error: current?.error ?? null,
  };
}

// For pages rendered inside AppShell: the capabilities it already loaded, without a second fetch.
// Use for presentation only (e.g. hiding an admin button) — apps/api enforces the real check.
export function useShellCapabilities() {
  return useOutletContext<Capabilities>();
}

export function hasRole(capabilities: Capabilities, role: string) {
  return capabilities.user.roles.includes(role);
}
