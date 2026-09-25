import type { TFunction } from "i18next";
import { ApiError } from "./client";

// One place that turns a failed apiFetch into user-visible text, so every page reports failures
// the same way instead of rendering an empty table or a default value as if it were real data.
// FR-404: the correlation ID is always shown when apps/api supplied one.
export function describeError(err: unknown, t: TFunction): string {
  let message: string;
  if (err instanceof ApiError) {
    message =
      err.status === 401
        ? t("errors.unauthorized")
        : err.status === 403
          ? t("errors.forbidden")
          : t("errors.generic", { status: err.status });
    if (err.correlationId) {
      message += ` ${t("errors.correlationId", { id: err.correlationId })}`;
    }
  } else {
    // fetch() itself rejects with a TypeError when apps/api is unreachable (down, CORS, network).
    message = t("errors.unreachable");
  }
  return message;
}
