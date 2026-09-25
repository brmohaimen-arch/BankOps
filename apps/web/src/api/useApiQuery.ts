import { useCallback, useEffect, useState } from "react";
import { useAuth } from "react-oidc-context";
import { apiFetch } from "./client";

interface QueryResult<T> {
  key: string;
  data: T | null;
  error: unknown;
}

// GET a JSON resource with the current access token. Loading is derived from "no result yet for
// this token + path + reload", so switching path (e.g. opening another service) shows loading
// instead of briefly rendering the previous resource's data. Pass null to fetch nothing.
export function useApiQuery<T>(path: string | null) {
  const auth = useAuth();
  const token = auth.user?.access_token;
  const [reloadCount, setReloadCount] = useState(0);
  const [result, setResult] = useState<QueryResult<T> | null>(null);
  const key = `${token}|${path}|${reloadCount}`;

  useEffect(() => {
    if (!token || !path) {
      return;
    }

    let cancelled = false;
    apiFetch(path, token)
      .then((response) => response.json())
      .then((data: T) => {
        if (!cancelled) setResult({ key, data, error: null });
      })
      .catch((error: unknown) => {
        if (!cancelled) setResult({ key, data: null, error });
      });

    return () => {
      cancelled = true;
    };
  }, [token, path, key]);

  const reload = useCallback(() => setReloadCount((n) => n + 1), []);
  const current = result?.key === key ? result : null;
  return {
    data: current?.data ?? null,
    error: current?.error ?? null,
    loading: path !== null && current === null,
    reload,
  };
}
