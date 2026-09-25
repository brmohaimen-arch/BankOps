import { useState } from "react";
import { Alert, Button, Empty, Flex, Spin, Table, Typography } from "antd";
import { useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useApiQuery } from "../api/useApiQuery";
import { describeError } from "../api/describeError";
import { hasRole, useShellCapabilities } from "../api/useCapabilities";
import type { ServiceSummary } from "../api/catalog";
import { CriticalityTag } from "../components/CriticalityTag";
import { UnknownHealth } from "../components/UnknownHealth";
import { ServiceDrawer } from "../components/ServiceDrawer";
import { RegisterServiceModal } from "../components/RegisterServiceModal";

// FR-101/FR-102 against modules/catalog. The open service lives in the URL (?service=<id>) so a
// service can be linked to directly and the browser back button closes the drawer.
export function CatalogPage() {
  const { t } = useTranslation();
  const capabilities = useShellCapabilities();
  const { data, error, loading, reload } = useApiQuery<ServiceSummary[]>("/api/v1/catalog/services");
  const [searchParams, setSearchParams] = useSearchParams();
  const [registering, setRegistering] = useState(false);
  const openServiceId = searchParams.get("service");

  const openService = (id: string | null) => setSearchParams(id ? { service: id } : {});

  return (
    <>
      <Flex justify="space-between" align="center">
        <Typography.Title level={3}>{t("catalog.title")}</Typography.Title>
        {hasRole(capabilities, "admin") && (
          <Button type="primary" onClick={() => setRegistering(true)}>
            {t("catalog.register.open")}
          </Button>
        )}
      </Flex>

      {loading ? (
        <Spin size="large" />
      ) : error ? (
        <Alert type="error" showIcon title={t("catalog.loadFailed")} description={describeError(error, t)} />
      ) : (
        <Table<ServiceSummary>
          rowKey="id"
          dataSource={data ?? []}
          pagination={{ pageSize: 25, hideOnSinglePage: true }}
          locale={{ emptyText: <Empty description={t("dashboard.emptyServices")} /> }}
          onRow={(service) => ({ onClick: () => openService(service.id), style: { cursor: "pointer" } })}
          columns={[
            { title: t("catalog.code"), dataIndex: "code" },
            { title: t("catalog.name"), dataIndex: "name" },
            { title: t("catalog.health"), render: () => <UnknownHealth /> },
            { title: t("catalog.criticality"), render: (_, s) => <CriticalityTag criticality={s.criticality} /> },
            { title: t("catalog.environment"), dataIndex: "environment" },
            { title: t("catalog.site"), dataIndex: "site", render: (site: string | null) => site ?? "—" },
            { title: t("catalog.owner"), dataIndex: "ownerRef", render: (owner: string | null) => owner ?? "—" },
          ]}
        />
      )}

      <ServiceDrawer serviceId={openServiceId} onClose={() => openService(null)} onOpenService={openService} />
      <RegisterServiceModal
        open={registering}
        onClose={() => setRegistering(false)}
        onCreated={() => {
          setRegistering(false);
          reload();
        }}
      />
    </>
  );
}
