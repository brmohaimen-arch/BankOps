import { useState } from "react";
import { Alert, Form, Input, Modal, Select } from "antd";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { apiFetch, ApiError } from "../api/client";
import { describeError } from "../api/describeError";
import { CRITICALITIES, type Criticality } from "../api/catalog";

interface FormValues {
  code: string;
  name: string;
  criticality: Criticality;
  environment: string;
  site?: string;
  ownerRef?: string;
}

// FR-101: register a service. Admin-only — the button that opens this is hidden for other roles,
// and apps/api rejects the POST with 403 regardless. Field rules are the server's (CatalogRules);
// its 400 field errors are shown against the matching inputs rather than duplicated here.
export function RegisterServiceModal({
  open,
  onClose,
  onCreated,
}: {
  open: boolean;
  onClose: () => void;
  onCreated: () => void;
}) {
  const { t } = useTranslation();
  const auth = useAuth();
  const [form] = Form.useForm<FormValues>();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const submit = async (values: FormValues) => {
    setSaving(true);
    setError(null);
    try {
      await apiFetch("/api/v1/catalog/services", auth.user?.access_token, {
        method: "POST",
        body: JSON.stringify(values),
      });
      form.resetFields();
      onCreated();
    } catch (err) {
      const body = err instanceof ApiError ? (err.body as { code?: string; errors?: Record<string, string[]> }) : undefined;
      if (err instanceof ApiError && err.status === 400 && body?.errors) {
        form.setFields(
          Object.entries(body.errors).map(([name, messages]) => ({ name: name as keyof FormValues, errors: messages })),
        );
      } else if (err instanceof ApiError && err.status === 409) {
        form.setFields([{ name: "code", errors: [t("catalog.register.codeTaken")] }]);
      } else {
        setError(err);
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      open={open}
      title={t("catalog.register.title")}
      okText={t("catalog.register.submit")}
      cancelText={t("cancel")}
      confirmLoading={saving}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {error !== null && <Alert type="error" showIcon title={describeError(error, t)} style={{ marginBottom: 16 }} />}
      <Form form={form} layout="vertical" onFinish={submit} initialValues={{ environment: "PROD", criticality: "MEDIUM" }}>
        <Form.Item name="code" label={t("catalog.code")} extra={t("catalog.register.codeHint")} rules={[{ required: true }]}>
          <Input placeholder="CORE-BANKING" />
        </Form.Item>
        <Form.Item name="name" label={t("catalog.name")} rules={[{ required: true }]}>
          <Input />
        </Form.Item>
        <Form.Item name="criticality" label={t("catalog.criticality")} rules={[{ required: true }]}>
          <Select options={CRITICALITIES.map((c) => ({ value: c, label: t(`criticality.${c}`) }))} />
        </Form.Item>
        <Form.Item name="environment" label={t("catalog.environment")} rules={[{ required: true }]}>
          <Input />
        </Form.Item>
        <Form.Item name="site" label={t("catalog.site")}>
          <Input />
        </Form.Item>
        <Form.Item name="ownerRef" label={t("catalog.owner")}>
          <Input placeholder="team:payments" />
        </Form.Item>
      </Form>
    </Modal>
  );
}
