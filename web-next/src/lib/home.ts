import "server-only";

import { redirect } from "next/navigation";
import { getSession } from "./session";
import { createTypedApiClient } from "./typed-api";
import { ApiError } from "./api";

export async function getTypedHomeDashboard() {
  const session = await getSession();

  if (!session) {
    redirect("/login");
  }

  const client = createTypedApiClient(session.accessToken);

  const { data, error, response } = await client.GET(
    "/api/v1/me/home",
  );

  if (response.status === 401) {
    redirect("/login?expired=1");
  }

  if (error || !data) {
    throw new ApiError(
      response.status,
      "Unable to load your stokvels.",
      response.headers.get("X-Correlation-ID"),
    );
  }

  return data;
}
