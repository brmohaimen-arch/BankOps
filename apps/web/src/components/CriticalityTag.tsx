import { Tag } from "antd";
import { useTranslation } from "react-i18next";
import type { Criticality } from "../api/catalog";

const COLOR_BY_CRITICALITY: Record<Criticality, string> = {
  CRITICAL: "red",
  HIGH: "orange",
  MEDIUM: "gold",
  LOW: "default",
};

export function CriticalityTag({ criticality }: { criticality: Criticality }) {
  const { t } = useTranslation();
  return <Tag color={COLOR_BY_CRITICALITY[criticality]}>{t(`criticality.${criticality}`)}</Tag>;
}
