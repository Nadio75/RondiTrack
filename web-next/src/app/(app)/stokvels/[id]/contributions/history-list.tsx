"use client";

import { useInfiniteQuery } from "@tanstack/react-query";
import {
  fetchContributionHistoryPage,
  type ClientContributionFilters,
} from "@/lib/contribution-history-client";
import type {
  PagedResponse,
  StokvelContributionHistoryItem,
} from "@/lib/types";

type HistoryListProps = {
  stokvelId: string;
  filters: ClientContributionFilters;
  initialPage: PagedResponse<StokvelContributionHistoryItem>;
};

const southAfricanDateFormatter = new Intl.DateTimeFormat("en-ZA", {
  timeZone: "Africa/Johannesburg",
  dateStyle: "medium",
  timeStyle: "short",
});

export function HistoryList({
  stokvelId,
  filters,
  initialPage,
}: HistoryListProps) {
  const query = useInfiniteQuery({
    queryKey: [
      "contribution-history",
      stokvelId,
      filters.member,
      filters.from,
      filters.to,
      filters.sort,
    ],
    initialPageParam: "",
    initialData: {
      pages: [initialPage],
      pageParams: [""],
    },
    queryFn: ({ pageParam, signal }) => {
      if (!pageParam) {
        return Promise.resolve(initialPage);
      }

      return fetchContributionHistoryPage(
        stokvelId,
        filters,
        pageParam,
        signal,
      );
    },
    getNextPageParam: (lastPage) =>
      lastPage.nextPageToken ?? undefined,
    refetchOnMount: false,
  });

  const contributions =
    query.data?.pages.flatMap((page) => page.items) ?? [];

  return (
    <section className="rounded-xl border bg-white p-6">
      <h2 className="text-xl font-semibold">
        Recorded contributions
      </h2>

      <p className="mt-2 text-sm text-gray-600">
        Showing {contributions.length} contribution
        {contributions.length === 1 ? "" : "s"}.
      </p>

      {contributions.length === 0 && (
        <p className="mt-4 text-sm text-gray-600">
          No contributions match these filters.
        </p>
      )}

      <ul className="mt-4 space-y-3">
        {contributions.map((contribution) => (
          <li
            key={contribution.id}
            className="rounded-lg border p-4"
          >
            <p className="font-medium">
              {contribution.memberName}
            </p>
            <p className="text-sm text-gray-600">
              Amount: {contribution.amount}
            </p>
            <p className="text-sm text-gray-600">
              {southAfricanDateFormatter.format(
                new Date(contribution.createdAt),
              )}
            </p>
          </li>
        ))}
      </ul>

      {query.isFetchNextPageError && (
        <p role="alert" className="mt-4 text-sm text-red-700">
          {query.error?.message ?? "Unable to load more contributions."}
        </p>
      )}

      {query.hasNextPage && (
        <button
          type="button"
          onClick={() => void query.fetchNextPage()}
          disabled={query.isFetchingNextPage}
          className="mt-5 rounded-lg border px-4 py-2 text-sm font-medium disabled:opacity-50"
        >
          {query.isFetchingNextPage ? "Loading more..." : "Load more"}
        </button>
      )}

      {!query.hasNextPage && contributions.length > 0 && (
        <p className="mt-4 text-sm text-gray-600">
          All available contributions are displayed.
        </p>
      )}
    </section>
  );
}
