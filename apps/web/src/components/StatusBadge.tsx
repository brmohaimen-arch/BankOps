import { Tag } from "antd";
import { useTranslation } from "react-i18next";

export type HealthStatus =
  | "healthy"
  | "warning"
  | "degraded"
  | "critical"
  | "unknown"
  | "stale"
  | "maintenance";

// README architectural decision #5: "Present a health result only with evidence, timestamp,
// source and freshness. Show Unknown/Stale when telemetry is missing; do not turn missing data
// into a green status." Color AND text together — frontend design doc: "Use words plus color and
// shape; red/green alone is inadequate."
const COLOR_BY_STATUS: Record<HealthStatus, string> = {
  healthy: "success",
  warning: "warning",
  degraded: "orange",
  critical: "error",
  unknown: "default",
  stale: "default",
  maintenance: "blue",
};

export function StatusBadge({ status }: { status: HealthStatus }) {
  const { t } = useTranslation();
  const label = status === "maintenance" ? "Maintenance" : t(`status.${status}`);
  return <Tag color={COLOR_BY_STATUS[status]}>{label}</Tag>;
}
