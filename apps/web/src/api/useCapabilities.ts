import { useEffect, useState } from "react";
import { useAuth } from "react-oidc-context";
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

// FR-002/frontend design: "Navigation is generated from a reviewed module manifest and an
// authorized server-supplied capability list." Hiding a menu item here is presentation only —
// apps/api independently rejects any route this doesn't list, same as every other endpoint.
export function useCapabilities() {
  const auth = useAuth();
  const [capabilities, setCapabilities] = useState<Capabilities | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    if (!auth.isAuthenticated || !auth.user?.access_token) {
      return;
    }

    let cancelled = false;
    setLoading(true);
    apiFetch("/api/v1/me/capabilities", auth.user.access_token)
      .then((response) => response.json())
      .then((data) => {
        if (!cancelled) {
          setCapabilities(data);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setError(err instanceof Error ? err : new Error(String(err)));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [auth.isAuthenticated, auth.user?.access_token]);

  return { capabilities, loading, error };
}
