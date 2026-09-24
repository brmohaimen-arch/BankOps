import { Layout, Menu, Tag, Button, Spin, Alert } from "antd";
import { useAuth } from "react-oidc-context";
import { useNavigate, useLocation, Outlet } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useCapabilities } from "../api/useCapabilities";
import { LanguageSwitch } from "./LanguageSwitch";

const { Header, Sider, Content } = Layout;

// Frontend design doc: "Persistent app shell with logo, application name, site/environment
// label, current user, language switch and global search... A visible environment badge
// prevents confusing lab and production."
export function AppShell() {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const { t, i18n } = useTranslation();
  const { capabilities, loading, error } = useCapabilities();

  if (loading || !capabilities) {
    return (
      <div style={{ display: "flex", justifyContent: "center", marginTop: 100 }}>
        <Spin size="large" tip={t("loading")} />
      </div>
    );
  }

  if (error) {
    return <Alert type="error" message="Could not load capabilities" description={error.message} showIcon />;
  }

  return (
    <Layout style={{ minHeight: "100vh" }}>
      <Header style={{ display: "flex", alignItems: "center", gap: 16, paddingInline: 24 }}>
        <span style={{ color: "white", fontWeight: 600, fontSize: 18 }}>{t("appName")}</span>
        <Tag color="orange">{t("environmentBadge")}</Tag>
        <div style={{ flex: 1 }} />
        <LanguageSwitch />
        <span style={{ color: "white" }}>{capabilities.user.subject}</span>
        <Button
          onClick={() =>
            // OpenIddict's end-session endpoint needs client_id to resolve which client's
            // registered post_logout_redirect_uri to validate against — it doesn't fall back to
            // the id_token_hint's aud/azp claim the way the OIDC spec allows. Without this,
            // /connect/logout rejects the request with "post_logout_redirect_uri is invalid" even
            // though the URI matches exactly what's registered.
            void auth.signoutRedirect({ extraQueryParams: { client_id: "bankops-web" } })
          }
        >
          {t("signOut")}
        </Button>
      </Header>
      <Layout dir={i18n.language === "ar" ? "rtl" : "ltr"}>
        <Sider width={220}>
          <Menu
            mode="inline"
            style={{ height: "100%" }}
            selectedKeys={[location.pathname]}
            items={capabilities.modules.map((m) => ({
              key: m.route,
              label: t(`nav.${m.id}`),
            }))}
            onClick={({ key }) => navigate(key)}
          />
        </Sider>
        <Content style={{ padding: 24 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
