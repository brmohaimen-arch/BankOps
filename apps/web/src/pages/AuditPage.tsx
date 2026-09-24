import { useEffect, useState } from "react";
import { Table, Typography, Spin } from "antd";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { apiFetch } from "../api/client";

interface AuditEntryDto {
  id: string;
  occurredAt: string;
  principalRef: string;
  action: string;
  resourceType: string;
}

// Real, against modules/audit's admin-only GET endpoint (built Phase 1). Shows every settings
// change made from the Appearance page — a live demonstration that changes are audited.
export function AuditPage() {
  const { t } = useTranslation();
  const auth = useAuth();
  const [entries, setEntries] = useState<AuditEntryDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const token = auth.user?.access_token;
    if (!token) return;
    apiFetch("/api/v1/audit", token)
      .then((r) => r.json())
      .then(setEntries)
      .finally(() => setLoading(false));
  }, [auth.user?.access_token]);

  return (
    <>
      <Typography.Title level={3}>{t("audit.title")}</Typography.Title>
      {loading ? (
        <Spin size="large" />
      ) : (
        <Table
          rowKey="id"
          dataSource={entries}
          columns={[
            { title: t("audit.occurredAt"), dataIndex: "occurredAt" },
            { title: t("audit.principal"), dataIndex: "principalRef" },
            { title: t("audit.action"), dataIndex: "action" },
            { title: "Resource type", dataIndex: "resourceType" },
          ]}
        />
      )}
    </>
  );
}
