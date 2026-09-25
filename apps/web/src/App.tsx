import { ConfigProvider } from "antd";
import enUS from "antd/locale/en_US";
import arEG from "antd/locale/ar_EG";
import { AuthProvider, useAuth } from "react-oidc-context";
import { BrowserRouter, Routes, Route } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useEffect, type ReactNode } from "react";
import { oidcConfig } from "./auth/oidcConfig";
import { AppShell } from "./shell/AppShell";
import { CallbackPage } from "./pages/CallbackPage";
import { DashboardPage } from "./pages/DashboardPage";
import { CatalogPage } from "./pages/CatalogPage";
import { AppearancePage } from "./pages/AppearancePage";
import { AuditPage } from "./pages/AuditPage";

function RequireAuth({ children }: { children: ReactNode }) {
  const auth = useAuth();
  const { t } = useTranslation();
  const needsSignIn = !auth.isLoading && !auth.isAuthenticated && !auth.activeNavigator && !auth.error;

  // Start the redirect from an effect, not during render — calling signinRedirect() while
  // rendering updates AuthProvider's state mid-render (React's "Cannot update a component while
  // rendering a different component" warning) and can fire more than once per navigation.
  useEffect(() => {
    if (needsSignIn) {
      void auth.signinRedirect();
    }
  }, [needsSignIn, auth]);

  if (auth.error) {
    // Without this, an unreachable IdP would leave a blank page (or retry in a loop).
    return <div style={{ padding: 24 }}>{t("signInFailed", { message: auth.error.message })}</div>;
  }

  if (!auth.isAuthenticated) {
    return null;
  }

  return <>{children}</>;
}

function AppRoutes() {
  const { i18n } = useTranslation();
  const direction = i18n.language === "ar" ? "rtl" : "ltr";

  return (
    <ConfigProvider direction={direction} locale={i18n.language === "ar" ? arEG : enUS}>
      <BrowserRouter>
        <Routes>
          <Route path="/callback" element={<CallbackPage />} />
          <Route
            path="/"
            element={
              <RequireAuth>
                <AppShell />
              </RequireAuth>
            }
          >
            <Route index element={<DashboardPage />} />
            <Route path="catalog" element={<CatalogPage />} />
            <Route path="admin/appearance" element={<AppearancePage />} />
            <Route path="admin/audit" element={<AuditPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </ConfigProvider>
  );
}

export default function App() {
  return (
    <AuthProvider {...oidcConfig}>
      <AppRoutes />
    </AuthProvider>
  );
}
