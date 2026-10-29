import type {
  ActivityFeedItem,
  PagedResponse,
} from "@/lib/types";

export async function fetchActivityPage(
  pageToken?: string,
  signal?: AbortSignal,
): Promise<PagedResponse<ActivityFeedItem>> {
  const params = new URLSearchParams({
    pageSize: "20",
  });

  if (pageToken) {
    params.set("pageToken", pageToken);
  }

  const response = await fetch(
    `/bff/activity?${params.toString()}`,
    {
      method: "GET",
      credentials: "same-origin",
      cache: "no-store",
      signal,
    },
  );

  if (!response.ok) {
    if (response.status === 401) {
      throw new Error(
        "Your session has expired. Please sign in again.",
      );
    }

    throw new Error(
      `Unable to load activity (${response.status}).`,
    );
  }

  return response.json() as Promise<
    PagedResponse<ActivityFeedItem>
  >;
}
