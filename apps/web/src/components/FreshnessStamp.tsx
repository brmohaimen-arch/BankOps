import { Tooltip } from "antd";

// Frontend design doc: "Every operational status has source, observed time, freshness and
// evidence." "Cached status always shows as-of timestamp; stale data is never silently passed as
// live" (FR-403). Hover shows the exact UTC instant — displayed time is always relative/local.
export function FreshnessStamp({ observedAt }: { observedAt: string }) {
  const date = new Date(observedAt);
  const ageSeconds = Math.max(0, (Date.now() - date.getTime()) / 1000);
  const relative =
    ageSeconds < 60
      ? "just now"
      : ageSeconds < 3600
        ? `${Math.floor(ageSeconds / 60)}m ago`
        : ageSeconds < 86400
          ? `${Math.floor(ageSeconds / 3600)}h ago`
          : `${Math.floor(ageSeconds / 86400)}d ago`;

  return (
    <Tooltip title={date.toISOString()}>
      <span style={{ color: "rgba(0,0,0,0.45)", fontSize: 12 }}>as of {relative}</span>
    </Tooltip>
  );
}
