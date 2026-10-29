"use client";

import { useTransition } from "react";
import { useQueryStates } from "nuqs";
import { contributionFilterParsers } from "@/lib/contribution-filters";
import type { MemberListItem } from "@/lib/types";

type HistoryFiltersProps = {
  members: MemberListItem[];
};

export function HistoryFilters({ members }: HistoryFiltersProps) {
  const [isPending, startTransition] = useTransition();

  const [filters, setFilters] = useQueryStates(
    contributionFilterParsers,
    {
      shallow: false,
      history: "push",
      clearOnDefault: true,
      startTransition,
    },
  );

  const invalidDateRange =
    Boolean(filters.from && filters.to && filters.from > filters.to);

  return (
    <section className="rounded-xl border bg-white p-6">
      <h2 className="mb-4 text-xl font-semibold">
        Filter contributions
      </h2>

      <div role="status" aria-live="polite" className="mb-4 text-sm">
        {isPending ? (
          <p className="font-medium text-blue-700">
            Updating results...
          </p>
        ) : (
          <p className="text-gray-500">
            Filters are ready.
          </p>
        )}
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <label className="flex flex-col gap-2 text-sm font-medium">
          Member
          <select
            value={filters.member}
            onChange={(event) =>
              void setFilters({ member: event.target.value })
            }
            className="rounded-lg border p-3"
          >
            <option value="">All members</option>
            {members.map((member) => (
              <option key={member.userId} value={member.userId}>
                {member.name}
              </option>
            ))}
          </select>
        </label>

        <label className="flex flex-col gap-2 text-sm font-medium">
          Sort order
          <select
            value={filters.sort}
            onChange={(event) =>
              void setFilters({ sort: event.target.value as "newest" | "oldest" })
            }
            className="rounded-lg border p-3"
          >
            <option value="newest">Newest first</option>
            <option value="oldest">Oldest first</option>
          </select>
        </label>

        <label className="flex flex-col gap-2 text-sm font-medium">
          From date (South African time)
          <input
            type="date"
            value={filters.from}
            onChange={(event) => {
              const from = event.target.value;

              void setFilters({
                from,
                ...(from && filters.to && from > filters.to
                  ? { to: "" }
                  : {}),
              });
            }}
            className="rounded-lg border p-3"
          />
        </label>

        <label className="flex flex-col gap-2 text-sm font-medium">
          To date (South African time)
          <input
            type="date"
            value={filters.to}
            onChange={(event) => {
              const to = event.target.value;

              void setFilters({
                to,
                ...(to && filters.from && to < filters.from
                  ? { from: "" }
                  : {}),
              });
            }}
            className="rounded-lg border p-3"
          />
        </label>
      </div>

      {invalidDateRange && (
        <p role="alert" className="mt-4 text-sm text-red-700">
          The start date cannot be after the end date.
        </p>
      )}

      <button
        type="button"
        onClick={() =>
          void setFilters({
            member: "",
            from: "",
            to: "",
            sort: "newest",
          })
        }
        className="mt-5 rounded-lg border px-4 py-2 text-sm font-medium"
      >
        Reset filters
      </button>
    </section>
  );
}
