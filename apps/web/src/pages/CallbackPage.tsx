import { useEffect } from "react";
import { Spin } from "antd";
import { useAuth } from "react-oidc-context";
import { useNavigate } from "react-router-dom";

// react-oidc-context processes the authorization_code + PKCE callback automatically once
// AuthProvider mounts and sees the code/state in the URL. This page just waits for that to finish
// and hands off to the router — using navigate() rather than history.replaceState so React
// Router's own state stays in sync (replaceState alone doesn't fire the popstate event Router
// listens for).
export function CallbackPage() {
  const auth = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (auth.isAuthenticated) {
      navigate("/", { replace: true });
    }
  }, [auth.isAuthenticated, navigate]);

  if (auth.error) {
    return <div style={{ padding: 24 }}>Sign-in failed: {auth.error.message}</div>;
  }

  return (
    <div style={{ display: "flex", justifyContent: "center", marginTop: 100 }}>
      <Spin size="large" />
    </div>
  );
}
