import { Suspense } from "react";
import { HistoryFilters } from "./history-filters";
import { HistoryList } from "./history-list";
import Link from "next/link";
import { notFound } from "next/navigation";
import { apiRequest, ApiError } from "@/lib/api";
import { loadContributionFilters } from "@/lib/contribution-filters";
import { getContributionHistory } from "@/lib/contribution-history";
import {
  getContributionHistoryMembers,
  isAllowedHistoryMember,
} from "@/lib/contribution-history-members";
import { findStokvelMembership } from "@/lib/stokvel-membership";
import type { HomeDashboard } from "@/lib/types";

type HistoryPageProps = {
  params: Promise<{ id: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function handleHistoryError(error: unknown): never {
  if (
    error instanceof ApiError &&
    (error.status === 403 || error.status === 404)
  ) {
    notFound();
  }

  throw error;
}

async function HistoryContent({
  params,
  searchParams,
}: HistoryPageProps) {
  const { id } = await params;

  if (
    !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)
  ) {
    notFound();
  }

  const home = await apiRequest<HomeDashboard>("/api/v1/me/home");
  const membership = findStokvelMembership(home, id);

  if (!membership) {
    notFound();
  }

  const parsed = loadContributionFilters(await searchParams);

  const filters = {
    member: parsed.member,
    from: parsed.from,
    to: parsed.to,
    sort: parsed.sort as "newest" | "oldest",
  };

  if (filters.from && filters.to && filters.from > filters.to) {
    filters.from = "";
    filters.to = "";
  }

  let members;

  try {
    members = await getContributionHistoryMembers(id);
  } catch (error) {
    handleHistoryError(error);
  }

  if (!isAllowedHistoryMember(filters.member, members)) {
    filters.member = "";
  }

  let history;

  try {
    history = await getContributionHistory(id, filters);
  } catch (error) {
    handleHistoryError(error);
  }


  return (
    <main className="space-y-6">
      <div>
        <Link
          href={`/stokvels/${id}`}
          className="text-sm text-blue-700 underline"
        >
          Back to stokvel
        </Link>

        <h1 className="mt-3 text-3xl font-bold">
          Contribution history
        </h1>

        <p className="mt-2 text-gray-600">
          {membership.name}
        </p>
      </div>

      <HistoryFilters members={members} />

      <HistoryList
        stokvelId={id}
        filters={filters}
        initialPage={history}
      />
    </main>
  );
}

export default function ContributionHistoryPage(
  props: HistoryPageProps,
) {
  return (
    <Suspense
      fallback={
        <div className="rounded-xl border bg-white p-6" role="status">
          Loading contribution history...
        </div>
      }
    >
      <HistoryContent {...props} />
    </Suspense>
  );
}
