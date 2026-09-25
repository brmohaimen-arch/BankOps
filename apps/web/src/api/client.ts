// apps/api — not proxied through anything, called directly cross-origin (see CORS setup in
// apps/api/Program.cs). Update this if a real deployment serves both from the same origin.
const API_BASE = "http://localhost:5065";

export class ApiError extends Error {
  status: number;
  correlationId: string | undefined;
  // Parsed JSON error body when apps/api sent one (e.g. { code, errors } on a 400), else undefined.
  body: unknown;

  constructor(status: number, correlationId: string | undefined, message: string, body?: unknown) {
    super(message);
    this.status = status;
    this.correlationId = correlationId;
    this.body = body;
  }
}

export async function apiFetch(
  path: string,
  accessToken: string | undefined,
  init?: RequestInit,
): Promise<Response> {
  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...init?.headers,
    },
  });

  if (!response.ok) {
    // FR-404: "one correlation ID for user-visible failures" — apps/api's GlobalExceptionHandler
    // puts it in the ProblemDetails body; CorrelationIdMiddleware also echoes it as a header.
    const correlationId = response.headers.get("X-Correlation-Id") ?? undefined;
    const body: unknown = await response.json().catch(() => undefined);
    throw new ApiError(response.status, correlationId, `${init?.method ?? "GET"} ${path} failed`, body);
  }

  return response;
}
