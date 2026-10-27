import "server-only";

import { redirect } from "next/navigation";
import { getSession } from "./session";

export type ApiProblem = {
  title?: string;
  detail?: string;
  status?: number;
  correlationId?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
};

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly correlationId: string | null = null,
    public readonly retryAfter: number | null = null,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export const apiBaseUrl =
  process.env.API_BASE_URL ?? "http://localhost:5056";

export async function createApiError(response: Response): Promise<ApiError> {
  let problem: ApiProblem | null = null;

  try {
    problem = (await response.clone().json()) as ApiProblem;
  } catch {
    // The API may return an empty response.
  }

  const validationMessage = Object.values(problem?.errors ?? {})
    .flat()
    .join(" ");

  const correlationId =
    response.headers.get("X-Correlation-ID") ??
    problem?.correlationId ??
    problem?.traceId ??
    null;

  const retryAfterHeader = response.headers.get("Retry-After");
  const retryAfter = retryAfterHeader
    ? Number(retryAfterHeader)
    : null;

  const message =
    validationMessage ||
    problem?.detail ||
    problem?.title ||
    `API request failed (${response.status}).`;

  return new ApiError(
    response.status,
    message,
    correlationId,
    retryAfter !== null && Number.isFinite(retryAfter)
      ? retryAfter
      : null,
  );
}

export async function apiRequest<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const session = await getSession();

  if (!session) {
    redirect("/login");
  }

  const headers = new Headers(options.headers);
  headers.set("Authorization", `Bearer ${session.accessToken}`);
  headers.set("Accept", "application/json");

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...options,
    headers,
    cache: "no-store",
  });

  if (response.status === 401) {
    redirect("/login?expired=1");
  }

  if (!response.ok) {
    throw await createApiError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
