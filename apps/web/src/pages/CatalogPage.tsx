import { Empty, Typography } from "antd";
import { useTranslation } from "react-i18next";

// modules/catalog's real endpoints don't exist yet (next item on documents/PHASE2_CHECKLIST.md) —
// this stays an honest empty state until they do, same reasoning as DashboardPage.
export function CatalogPage() {
  const { t } = useTranslation();
  return (
    <>
      <Typography.Title level={3}>{t("catalog.title")}</Typography.Title>
      <Empty description={t("dashboard.emptyServices")} />
    </>
  );
}
