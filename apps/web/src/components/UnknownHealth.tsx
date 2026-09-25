import { Tooltip } from "antd";
import { useTranslation } from "react-i18next";
import { StatusBadge } from "./StatusBadge";

// README architectural decision #5: missing telemetry is shown as Unknown, never as a green status.
// No monitoring source exists yet (modules/monitoring is Phase 3), so this is every service's
// honest health today — the tooltip says why, so Unknown isn't mistaken for a fault.
export function UnknownHealth() {
  const { t } = useTranslation();
  return (
    <Tooltip title={t("health.noSource")}>
      <span>
        <StatusBadge status="unknown" />
      </span>
    </Tooltip>
  );
}
