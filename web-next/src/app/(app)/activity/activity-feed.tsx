"use client";

import { useEffect, useRef } from "react";
import { useInfiniteQuery } from "@tanstack/react-query";
import { fetchActivityPage } from "@/lib/activity-client";
import type {
  ActivityFeedItem,
  PagedResponse,
} from "@/lib/types";

type ActivityFeedProps = {
  initialPage: PagedResponse<ActivityFeedItem>;
};

export function ActivityFeed({
  initialPage,
}: ActivityFeedProps) {
  const loadMoreRef = useRef<HTMLDivElement>(null);

  const {
    data,
    error,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
    isPending,
  } = useInfiniteQuery({
    queryKey: ["member-activity"],
    initialPageParam: "",
    initialData: {
      pages: [initialPage],
      pageParams: [""],
    },
    queryFn: ({ pageParam, signal }) =>
      fetchActivityPage(pageParam || undefined, signal),
    getNextPageParam: (lastPage) =>
      lastPage.nextPageToken ?? undefined,
    refetchOnMount: false,
  });

  const activities =
    data?.pages.flatMap((page) => page.items) ?? [];

  useEffect(() => {
    const element = loadMoreRef.current;

    if (!element || !hasNextPage) {
      return;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        if (
          entries[0]?.isIntersecting &&
          hasNextPage &&
          !isFetchingNextPage &&
          !error
        ) {
          void fetchNextPage();
        }
      },
      {
        rootMargin: "200px",
      },
    );

    observer.observe(element);

    return () => observer.disconnect();
  }, [
    hasNextPage,
    isFetchingNextPage,
    error,
    fetchNextPage,
  ]);

  return (
    <section
      aria-label="Activity history"
      className="rounded-lg border bg-white p-5"
    >
      <p className="mb-4 text-sm text-gray-600" role="status">
        Showing {activities.length}{" "}
        {activities.length === 1 ? "activity" : "activities"}
      </p>

      {isPending ? (
        <p>Loading activities...</p>
      ) : activities.length === 0 ? (
        <p className="py-8 text-center text-gray-600">
          No activity recorded yet.
        </p>
      ) : (
        <ul className="divide-y">
          {activities.map((item) => (
            <li
              key={`${item.activityType}-${item.id}`}
              className="py-4"
            >
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="font-semibold capitalize">
                    {item.activityType}
                  </p>

                  <p className="text-sm text-gray-700">
                    {item.stokvelName}
                  </p>

                  {item.memberName && (
                    <p className="text-sm text-gray-500">
                      Member: {item.memberName}
                    </p>
                  )}
                </div>

                {item.amount !== null && (
                  <p className="font-semibold">
                    R {item.amount.toFixed(2)}
                  </p>
                )}
              </div>

              <time
                dateTime={item.occurredAt}
                className="mt-2 block text-sm text-gray-500"
              >
                {new Intl.DateTimeFormat("en-ZA", {
                  dateStyle: "medium",
                  timeStyle: "short",
                  timeZone: "Africa/Johannesburg",
                }).format(new Date(item.occurredAt))}
              </time>
            </li>
          ))}
        </ul>
      )}

      {error && (
        <p role="alert" className="mt-4 text-red-700">
          {error.message}
        </p>
      )}

      {hasNextPage && (
        <div ref={loadMoreRef} className="mt-5">
          <button
            type="button"
            onClick={() => void fetchNextPage()}
            disabled={isFetchingNextPage}
            className="rounded-md bg-blue-700 px-4 py-2 text-white disabled:opacity-50"
          >
            {isFetchingNextPage ? "Loading more..." : "Load more"}
          </button>
        </div>
      )}

      {!hasNextPage && activities.length > 0 && (
        <p className="mt-4 text-sm text-gray-500">
          All available activities are displayed.
        </p>
      )}
    </section>
  );
}
