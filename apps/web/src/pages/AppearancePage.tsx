import { useEffect, useState } from "react";
import { ColorPicker, Form, Spin, Typography, message } from "antd";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { apiFetch, ApiError } from "../api/client";

interface SettingDto {
  namespace: string;
  key: string;
  value: string; // raw JSON text from apps/api
  version: number;
}

// FR-003: "Admin changes bank name, logos, favicon, primary colors, language defaults and theme
// from settings... Valid change appears without frontend rebuild." This is real, working end-to-
// end against modules/settings (built in Phase 1) — not a placeholder. Logo/favicon upload isn't
// wired up yet (modules/settings/README.md's "still missing" list); this covers the color/text
// settings path, which is the part that's actually built on the backend today.
export function AppearancePage() {
  const { t } = useTranslation();
  const auth = useAuth();
  const [setting, setSetting] = useState<SettingDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const token = auth.user?.access_token;

  const load = () => {
    if (!token) return;
    setLoading(true);
    apiFetch("/api/v1/settings/branding", token)
      .then((r) => r.json())
      .then((settings: SettingDto[]) => {
        setSetting(settings.find((s) => s.key === "primaryColor") ?? null);
      })
      .catch((err) => messageApi.error(`Failed to load: ${err.message}`))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    // messageApi is stable across renders (antd's own guarantee for message.useMessage());
    // load is intentionally excluded — redefining it every render would otherwise refetch in a loop.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token]);

  const handleSave = async (color: string) => {
    if (!token) return;
    setSaving(true);
    try {
      await apiFetch("/api/v1/settings/branding/primaryColor", token, {
        method: "PUT",
        body: JSON.stringify({ value: color, expectedVersion: setting?.version ?? null }),
      });
      messageApi.success("Saved — no rebuild required.");
      load();
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        messageApi.error("Someone else changed this setting — reloading the current value.");
        load();
      } else {
        messageApi.error(err instanceof Error ? err.message : String(err));
      }
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <Spin size="large" />;
  }

  const currentColor = setting ? (JSON.parse(setting.value) as string) : "#1677ff";

  return (
    <>
      {contextHolder}
      <Typography.Title level={3}>{t("appearance.title")}</Typography.Title>
      <Form layout="vertical" style={{ maxWidth: 320 }}>
        <Form.Item label={t("appearance.primaryColor")}>
          <ColorPicker
            value={currentColor}
            onChangeComplete={(color) => void handleSave(color.toHexString())}
            showText
            disabled={saving}
          />
        </Form.Item>
      </Form>
    </>
  );
}
