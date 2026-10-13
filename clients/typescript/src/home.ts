import type { paths } from "./generated/api.js";

type HomeResponse =
  paths["/api/v1/me/home"]["get"]["responses"][200]["content"]["application/json"];

export async function getHomeDashboard(
  baseUrl: string,
  accessToken: string
): Promise<HomeResponse> {
  const response = await fetch(
    `${baseUrl.replace(/\/$/, "")}/api/v1/me/home`,
    {
      method: "GET",
      headers: {
        Accept: "application/json",
        Authorization: `Bearer ${accessToken}`,
      },
    }
  );

  if (!response.ok) {
    throw new Error(
      `Failed to load home dashboard: HTTP ${response.status}`
    );
  }

  return (await response.json()) as HomeResponse;
}