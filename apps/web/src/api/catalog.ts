// Shapes returned by modules/catalog (apps/api /api/v1/catalog). Health is not part of these on
// purpose — catalog doesn't own it; screens show Unknown until modules/monitoring supplies evidence.

export type Criticality = "CRITICAL" | "HIGH" | "MEDIUM" | "LOW";
export const CRITICALITIES: Criticality[] = ["CRITICAL", "HIGH", "MEDIUM", "LOW"];

export interface ServiceSummary {
  id: string;
  code: string;
  name: string;
  ownerRef: string | null;
  criticality: Criticality;
  environment: string;
  site: string | null;
  graphVersion: number;
  updatedAt: string;
}

export interface Dependency {
  targetKind: string;
  targetId: string;
  targetCode: string | null;
  targetName: string | null;
  relation: string;
  isCritical: boolean;
}

export interface Dependent {
  serviceId: string;
  code: string;
  name: string;
  relation: string;
  isCritical: boolean;
}

export interface ServiceDetail {
  service: ServiceSummary;
  dependencies: Dependency[];
  dependents: Dependent[];
}
