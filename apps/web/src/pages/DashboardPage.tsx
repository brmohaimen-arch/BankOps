import { Empty, Typography } from "antd";
import { useTranslation } from "react-i18next";

// Real dashboard widgets need modules/catalog + modules/monitoring, neither of which have real
// data yet (Phase 2/3 work in progress). An honest empty state here, not fabricated mock rows —
// README architectural decision #5 explicitly warns against ever dressing up missing data as a
// status. Wire this up once modules/catalog exists.
export function DashboardPage() {
  const { t } = useTranslation();
  return (
    <>
      <Typography.Title level={3}>{t("dashboard.title")}</Typography.Title>
      <Empty description={t("dashboard.emptyServices")} />
    </>
  );
}
