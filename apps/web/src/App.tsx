import { ConfigProvider } from "antd";
import enUS from "antd/locale/en_US";
import arEG from "antd/locale/ar_EG";
import { AuthProvider, useAuth } from "react-oidc-context";
import { BrowserRouter, Routes, Route } from "react-router-dom";
import { useTranslation } from "react-i18next";
import type { ReactNode } from "react";
import { oidcConfig } from "./auth/oidcConfig";
import { AppShell } from "./shell/AppShell";
import { CallbackPage } from "./pages/CallbackPage";
import { DashboardPage } from "./pages/DashboardPage";
import { CatalogPage } from "./pages/CatalogPage";
import { AppearancePage } from "./pages/AppearancePage";
import { AuditPage } from "./pages/AuditPage";

function RequireAuth({ children }: { children: ReactNode }) {
  const auth = useAuth();

  if (auth.isLoading) {
    return null;
  }

  if (!auth.isAuthenticated) {
    void auth.signinRedirect();
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
