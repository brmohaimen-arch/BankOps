import { Alert, Descriptions, Drawer, Empty, Spin, Table, Tag, Typography } from "antd";
import { useTranslation } from "react-i18next";
import { useApiQuery } from "../api/useApiQuery";
import { describeError } from "../api/describeError";
import type { Dependency, Dependent, ServiceDetail } from "../api/catalog";
import { CriticalityTag } from "./CriticalityTag";
import { UnknownHealth } from "./UnknownHealth";

// One service's published dependency set (what it needs) and its dependents (what needs it) —
// FR-102's graph, one hop at a time. Clicking a related service opens that service instead.
export function ServiceDrawer({
  serviceId,
  onClose,
  onOpenService,
}: {
  serviceId: string | null;
  onClose: () => void;
  onOpenService: (id: string) => void;
}) {
  const { t } = useTranslation();
  const { data, error, loading } = useApiQuery<ServiceDetail>(
    serviceId ? `/api/v1/catalog/services/${serviceId}` : null,
  );

  const serviceLink = (id: string, code: string) => (
    <Typography.Link onClick={() => onOpenService(id)}>{code}</Typography.Link>
  );

  return (
    <Drawer open={serviceId !== null} onClose={onClose} size="large" title={data?.service.name ?? t("catalog.title")}>
      {loading ? (
        <Spin size="large" />
      ) : error ? (
        <Alert type="error" showIcon title={t("catalog.detailFailed")} description={describeError(error, t)} />
      ) : data ? (
        <>
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label={t("catalog.code")}>{data.service.code}</Descriptions.Item>
            <Descriptions.Item label={t("catalog.health")}>
              <UnknownHealth />
            </Descriptions.Item>
            <Descriptions.Item label={t("catalog.criticality")}>
              <CriticalityTag criticality={data.service.criticality} />
            </Descriptions.Item>
            <Descriptions.Item label={t("catalog.environment")}>{data.service.environment}</Descriptions.Item>
            <Descriptions.Item label={t("catalog.site")}>{data.service.site ?? "—"}</Descriptions.Item>
            <Descriptions.Item label={t("catalog.owner")}>{data.service.ownerRef ?? "—"}</Descriptions.Item>
            <Descriptions.Item label={t("catalog.graphVersion")}>{data.service.graphVersion}</Descriptions.Item>
          </Descriptions>

          <Typography.Title level={5} style={{ marginTop: 24 }}>
            {t("catalog.dependsOn")}
          </Typography.Title>
          <Table<Dependency>
            size="small"
            pagination={false}
            rowKey={(d) => `${d.targetKind}:${d.targetId}:${d.relation}`}
            dataSource={data.dependencies}
            locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t("catalog.noDependencies")} /> }}
            columns={[
              {
                title: t("catalog.service"),
                render: (_, d) =>
                  d.targetCode ? (
                    <>
                      {serviceLink(d.targetId, d.targetCode)} <Typography.Text type="secondary">{d.targetName}</Typography.Text>
                    </>
                  ) : (
                    <Typography.Text type="secondary">
                      {d.targetKind} {d.targetId}
                    </Typography.Text>
                  ),
              },
              { title: t("catalog.relation"), render: (_, d) => t(`relation.${d.relation}`) },
              { title: t("catalog.critical"), render: (_, d) => (d.isCritical ? <Tag color="red">{t("yes")}</Tag> : t("no")) },
            ]}
          />

          <Typography.Title level={5} style={{ marginTop: 24 }}>
            {t("catalog.neededBy")}
          </Typography.Title>
          <Table<Dependent>
            size="small"
            pagination={false}
            rowKey={(d) => `${d.serviceId}:${d.relation}`}
            dataSource={data.dependents}
            locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t("catalog.noDependents")} /> }}
            columns={[
              {
                title: t("catalog.service"),
                render: (_, d) => (
                  <>
                    {serviceLink(d.serviceId, d.code)} <Typography.Text type="secondary">{d.name}</Typography.Text>
                  </>
                ),
              },
              { title: t("catalog.relation"), render: (_, d) => t(`relation.${d.relation}`) },
              { title: t("catalog.critical"), render: (_, d) => (d.isCritical ? <Tag color="red">{t("yes")}</Tag> : t("no")) },
            ]}
          />
        </>
      ) : null}
    </Drawer>
  );
}
