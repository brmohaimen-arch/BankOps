import { useEffect, useState } from "react";
import { Tooltip } from "antd";
import { useTranslation } from "react-i18next";

// Re-render periodically so "5m ago" keeps aging while the page stays open. Reading the clock in
// state (not Date.now() during render) keeps render pure.
function useNow(intervalMs = 30_000) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);
  return now;
}

// Frontend design doc: "Every operational status has source, observed time, freshness and
// evidence." "Cached status always shows as-of timestamp; stale data is never silently passed as
// live" (FR-403). Hover shows the exact UTC instant — displayed time is always relative/local.
export function FreshnessStamp({ observedAt }: { observedAt: string }) {
  const { t } = useTranslation();
  const now = useNow();
  const date = new Date(observedAt);
  const ageSeconds = Math.max(0, (now - date.getTime()) / 1000);
  const relative =
    ageSeconds < 60
      ? t("freshness.justNow")
      : ageSeconds < 3600
        ? t("freshness.minutesAgo", { count: Math.floor(ageSeconds / 60) })
        : ageSeconds < 86400
          ? t("freshness.hoursAgo", { count: Math.floor(ageSeconds / 3600) })
          : t("freshness.daysAgo", { count: Math.floor(ageSeconds / 86400) });

  return (
    <Tooltip title={date.toISOString()}>
      <span style={{ color: "rgba(0,0,0,0.45)", fontSize: 12 }}>{t("freshness.asOf", { relative })}</span>
    </Tooltip>
  );
}
