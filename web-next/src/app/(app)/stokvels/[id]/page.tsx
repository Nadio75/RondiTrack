import { Suspense } from "react";
import { ContributionForm } from "./contribution-form";
import { notFound } from "next/navigation";
import { apiRequest, ApiError } from "@/lib/api";
import { getServerTime } from "@/lib/server-timing";
import { findStokvelMembership } from "@/lib/stokvel-membership";
import type {
  HomeDashboard,
  HomeStokvel,
  StokvelDetail,
  MemberListItem,
  ContributionListItem,
  PagedResponse,
} from "@/lib/types";

function handleNotFound(error: unknown): never {
  if (
    error instanceof ApiError &&
    (error.status === 403 || error.status === 404)
  ) {
    notFound();
  }

  throw error;
}

async function RecentContributions({
  stokvelId,
  cycleId,
}: {
  stokvelId: string;
  cycleId: string;
}) {
  let contributions: PagedResponse<ContributionListItem>;

  try {
    contributions = await apiRequest<PagedResponse<ContributionListItem>>(
      `/api/v1/stokvels/${encodeURIComponent(stokvelId)}` +
        `/cycles/${encodeURIComponent(cycleId)}` +
        `/contributions?page_size=5&sort=createdAt`,
    );
  } catch (error) {
    handleNotFound(error);
  }

  return (
    <div className="rounded-xl border bg-white p-6">
      <h2 className="mb-3 text-xl font-semibold">
        Recent contributions
      </h2>

      {contributions.items.length === 0 ? (
        <p>No contributions have been recorded yet.</p>
      ) : (
        <ul className="space-y-3">
          {contributions.items.map((contribution) => (
            <li key={contribution.id} className="border-b pb-3">
              <p className="font-medium">{contribution.memberName}</p>
              <p>Amount: {contribution.amount}</p>
              <p>
                Date: {new Date(contribution.createdAt).toLocaleDateString("en-ZA")}
              </p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function ContributionsSkeleton() {
  return (
    <div className="rounded-xl border bg-white p-6" role="status">
      <h2 className="mb-3 text-xl font-semibold">
        Recent contributions
      </h2>
      <p>Loading recent contributions...</p>
    </div>
  );
}

async function StokvelDetailContent({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  if (
    !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)
  ) {
    notFound();
  }

  const startedAt = getServerTime();
  const homePromise = apiRequest<HomeDashboard>("/api/v1/me/home");

  const stokvelPromise = apiRequest<StokvelDetail>(
    `/api/v1/stokvels/${encodeURIComponent(id)}`,
  );

  const membersPromise = apiRequest<PagedResponse<MemberListItem>>(
    `/api/v1/stokvels/${encodeURIComponent(id)}/members?page_size=20`,
  );

  let membership: HomeStokvel;

  try {
    const home = await homePromise;

    const found = findStokvelMembership(home, id);

    if (!found) {
      notFound();
    }

    membership = found;
  } catch (error) {
    await Promise.allSettled([stokvelPromise, membersPromise]);
    throw error;
  }

  let stokvel: StokvelDetail;
  let members: PagedResponse<MemberListItem>;

  try {
    [stokvel, members] = await Promise.all([
      stokvelPromise,
      membersPromise,
    ]);
  } catch (error) {
    handleNotFound(error);
  }

  console.info("[StokvelDetail] Main data loaded in", Math.round(getServerTime() - startedAt), "ms");

  return (
    <section className="space-y-6">
      <h1 className="text-3xl font-bold">{stokvel.name}</h1>

      <div className="rounded-xl border bg-white p-6">
        <p>Your role: {membership.role}</p>
        <p>Contribution amount: {stokvel.contributionAmount}</p>
        <p>Members: {stokvel.memberCount}</p>
      </div>

      <div className="rounded-xl border bg-white p-6">
        <h2 className="mb-3 text-xl font-semibold">Current cycle</h2>

        {membership.currentCycle ? (
          <div className="space-y-2">
            <p>Period: {membership.currentCycle.period}</p>
            <p>Status: {membership.currentCycle.status}</p>
            <p>
              Payment: {membership.currentCycle.hasPaid ? "Paid" : "Not paid"}
            </p>
            <p>Amount owed: {membership.currentCycle.amountOwed}</p>
          </div>
        ) : (
          <p>No current contribution cycle is available.</p>
        )}
      </div>

      {membership.currentCycle &&
        membership.currentCycle.status === "Open" &&
        !membership.currentCycle.hasPaid && (
        <ContributionForm
          stokvelId={id}
          cycleId={membership.currentCycle.cycleId}
        />
      )}

      <div className="rounded-xl border bg-white p-6">
        <h2 className="mb-3 text-xl font-semibold">Members</h2>

        {members.items.length === 0 ? (
          <p>No members found.</p>
        ) : (
          <ul className="space-y-2">
            {members.items.map((member) => (
              <li key={member.userId}>
                {member.name} - {member.role}
              </li>
            ))}
          </ul>
        )}
      </div>

      {membership.currentCycle ? (
        <Suspense fallback={<ContributionsSkeleton />}>
          <RecentContributions
            stokvelId={id}
            cycleId={membership.currentCycle.cycleId}
          />
        </Suspense>
      ) : (
        <div className="rounded-xl border bg-white p-6">
          <h2 className="mb-3 text-xl font-semibold">
            Recent contributions
          </h2>
          <p>No active cycle to display contributions for.</p>
        </div>
      )}
    </section>
  );
}

export default function StokvelDetailPage({
  params,
}: PageProps<"/stokvels/[id]">) {
  return (
    <Suspense fallback={<p>Loading stokvel details...</p>}>
      <StokvelDetailContent params={params} />
    </Suspense>
  );
}


