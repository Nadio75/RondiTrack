import { southAfricanDaysToUtcRange } from "@/lib/south-african-dates";
import type {
  PagedResponse,
  StokvelContributionHistoryItem,
} from "@/lib/types";

export type ClientContributionFilters = {
  member: string;
  from: string;
  to: string;
  sort: "newest" | "oldest";
};

export async function fetchContributionHistoryPage(
  stokvelId: string,
  filters: ClientContributionFilters,
  pageToken: string,
  signal?: AbortSignal,
): Promise<PagedResponse<StokvelContributionHistoryItem>> {
  const { fromUtc, toUtc } = southAfricanDaysToUtcRange(
    filters.from || undefined,
    filters.to || undefined,
  );

  const parameters = new URLSearchParams({
    sort: filters.sort,
    pageSize: "20",
    pageToken,
  });

  if (filters.member) {
    parameters.set("member", filters.member);
  }

  if (fromUtc) {
    parameters.set("from", fromUtc);
  }

  if (toUtc) {
    parameters.set("to", toUtc);
  }

  const response = await fetch(
    `/bff/stokvels/${encodeURIComponent(stokvelId)}` +
      `/contributions?${parameters.toString()}`,
    {
      method: "GET",
      credentials: "same-origin",
      cache: "no-store",
      signal,
    },
  );

  if (!response.ok) {
    throw new Error(
      response.status === 401
        ? "Your session has expired. Please sign in again."
        : "Unable to load more contributions.",
    );
  }

  return (await response.json()) as PagedResponse<StokvelContributionHistoryItem>;
}
