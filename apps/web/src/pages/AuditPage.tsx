import { useEffect, useState } from "react";
import { Alert, Table, Typography, Spin } from "antd";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { apiFetch } from "../api/client";
import { describeError } from "../api/describeError";

interface AuditEntryDto {
  id: string;
  occurredAt: string;
  principalRef: string;
  action: string;
  resourceType: string;
}

interface AuditResult {
  entries: AuditEntryDto[] | null;
  error: unknown;
}

// Real, against modules/audit's admin-only GET endpoint (built Phase 1). Shows every settings
// change made from the Appearance page — a live demonstration that changes are audited.
export function AuditPage() {
  const { t } = useTranslation();
  const auth = useAuth();
  const [result, setResult] = useState<AuditResult | null>(null);

  useEffect(() => {
    const token = auth.user?.access_token;
    if (!token) return;
    let cancelled = false;
    apiFetch("/api/v1/audit", token)
      .then((r) => r.json())
      .then((entries: AuditEntryDto[]) => {
        if (!cancelled) setResult({ entries, error: null });
      })
      // A failed load (e.g. a viewer's 403 after opening this route by URL) must say so — an
      // empty table would read as "nothing has been audited", which is a false statement.
      .catch((error: unknown) => {
        if (!cancelled) setResult({ entries: null, error });
      });
    return () => {
      cancelled = true;
    };
  }, [auth.user?.access_token]);

  return (
    <>
      <Typography.Title level={3}>{t("audit.title")}</Typography.Title>
      {result === null ? (
        <Spin size="large" />
      ) : result.error ? (
        <Alert type="error" showIcon title={t("audit.loadFailed")} description={describeError(result.error, t)} />
      ) : (
        <Table
          rowKey="id"
          dataSource={result.entries ?? []}
          columns={[
            { title: t("audit.occurredAt"), dataIndex: "occurredAt" },
            { title: t("audit.principal"), dataIndex: "principalRef" },
            { title: t("audit.action"), dataIndex: "action" },
            { title: t("audit.resourceType"), dataIndex: "resourceType" },
          ]}
        />
      )}
    </>
  );
}
