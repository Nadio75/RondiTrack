import "server-only";

import createClient from "openapi-fetch";
import type { paths } from "./schema";

export function createTypedApiClient(accessToken: string) {
  const baseUrl = process.env.API_BASE_URL;

  if (!baseUrl) {
    throw new Error("API_BASE_URL is not configured.");
  }

  return createClient<paths>({
    baseUrl,
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
    fetch: (request) =>
      fetch(request, {
        cache: "no-store",
      }),
  });
}
