import "server-only";

import { apiRequest } from "@/lib/api";
import { southAfricanDaysToUtcRange } from "@/lib/south-african-dates";
import type {
  PagedResponse,
  StokvelContributionHistoryItem,
} from "@/lib/types";

export type ContributionHistoryFilters = {
  member: string;
  from: string;
  to: string;
  sort: "newest" | "oldest";
};

export async function getContributionHistory(
  stokvelId: string,
  filters: ContributionHistoryFilters,
  pageToken?: string,
): Promise<PagedResponse<StokvelContributionHistoryItem>> {
  const { fromUtc, toUtc } = southAfricanDaysToUtcRange(
    filters.from || undefined,
    filters.to || undefined,
  );

  const params = new URLSearchParams();

  if (filters.member) {
    params.set("member", filters.member);
  }

  if (fromUtc) {
    params.set("from", fromUtc);
  }

  if (toUtc) {
    params.set("to", toUtc);
  }

  params.set("sort", filters.sort);
  params.set("pageSize", "20");

  if (pageToken) {
    params.set("pageToken", pageToken);
  }

  const path =
    `/api/v1/stokvels/${encodeURIComponent(stokvelId)}/contributions` +
    `?${params.toString()}`;

  return apiRequest<PagedResponse<StokvelContributionHistoryItem>>(
    path,
  );
}
