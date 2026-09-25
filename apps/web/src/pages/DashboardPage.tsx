import { Alert, Card, Col, Empty, Row, Spin, Statistic, Table, Typography } from "antd";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useApiQuery } from "../api/useApiQuery";
import { describeError } from "../api/describeError";
import { CRITICALITIES, type ServiceSummary } from "../api/catalog";
import { CriticalityTag } from "../components/CriticalityTag";
import { UnknownHealth } from "../components/UnknownHealth";

// Overview built from modules/catalog. Health counts are deliberately not "N healthy": with no
// monitoring source yet (modules/monitoring, Phase 3), every service's health is Unknown, and the
// dashboard says so plainly instead of rendering an empty or green status — README architectural
// decision #5.
export function DashboardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data, error, loading } = useApiQuery<ServiceSummary[]>("/api/v1/catalog/services");

  if (loading) {
    return <Spin size="large" />;
  }

  if (error || !data) {
    return (
      <>
        <Typography.Title level={3}>{t("dashboard.title")}</Typography.Title>
        <Alert type="error" showIcon title={t("catalog.loadFailed")} description={describeError(error, t)} />
      </>
    );
  }

  if (data.length === 0) {
    return (
      <>
        <Typography.Title level={3}>{t("dashboard.title")}</Typography.Title>
        <Empty description={t("dashboard.emptyServices")} />
      </>
    );
  }

  const critical = data.filter((s) => s.criticality === "CRITICAL");

  return (
    <>
      <Typography.Title level={3}>{t("dashboard.title")}</Typography.Title>

      <Alert type="info" showIcon title={t("dashboard.healthUnknownTitle")} description={t("dashboard.healthUnknownBody")} style={{ marginBottom: 16 }} />

      <Row gutter={[16, 16]}>
        <Col xs={24} md={8}>
          <Card>
            <Statistic title={t("dashboard.registeredServices")} value={data.length} />
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card>
            <Statistic title={t("dashboard.healthUnknownCount")} value={data.length} suffix={`/ ${data.length}`} />
          </Card>
        </Col>
        <Col xs={24} md={8}>
          <Card title={t("dashboard.byCriticality")} size="small">
            {CRITICALITIES.map((c) => (
              <Row key={c} justify="space-between" style={{ marginBottom: 4 }}>
                <CriticalityTag criticality={c} />
                <span>{data.filter((s) => s.criticality === c).length}</span>
              </Row>
            ))}
          </Card>
        </Col>
      </Row>

      <Typography.Title level={5} style={{ marginTop: 24 }}>
        {t("dashboard.criticalServices")}
      </Typography.Title>
      <Table<ServiceSummary>
        size="small"
        rowKey="id"
        pagination={false}
        dataSource={critical}
        onRow={(s) => ({ onClick: () => navigate(`/catalog?service=${s.id}`), style: { cursor: "pointer" } })}
        columns={[
          { title: t("catalog.code"), dataIndex: "code" },
          { title: t("catalog.name"), dataIndex: "name" },
          { title: t("catalog.health"), render: () => <UnknownHealth /> },
          { title: t("catalog.site"), dataIndex: "site", render: (site: string | null) => site ?? "—" },
        ]}
      />
    </>
  );
}
