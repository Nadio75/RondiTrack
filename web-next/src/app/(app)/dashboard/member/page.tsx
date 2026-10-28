import { Suspense } from "react";
import Link from "next/link";
import { apiRequest } from "@/lib/api";
import type { HomeDashboard } from "@/lib/types";

async function MemberDashboardPageContent() {
  const home = await apiRequest<HomeDashboard>("/api/v1/me/home");

  return (
    <section className="space-y-6">
      <h1 className="text-3xl font-bold">Member Dashboard</h1>

      {home.stokvels.length === 0 ? (
        <p>You have no stokvel memberships yet.</p>
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          {home.stokvels.map((stokvel) => (
            <article
              key={stokvel.stokvelId}
              className="space-y-2 rounded-xl border bg-white p-6"
            >
              <h2 className="text-xl font-semibold">{stokvel.name}</h2>
              <p>Role: {stokvel.role}</p>
              <p>Contribution: {stokvel.contributionAmount}</p>
              <p>
                Payment:{" "}
                {stokvel.currentCycle
                  ? stokvel.currentCycle.hasPaid
                    ? "Paid"
                    : "Not paid"
                  : "No active cycle"}
              </p>
              <p>
                Amount owed:{" "}
                {stokvel.currentCycle?.amountOwed ?? "Not available"}
              </p>
              <p>
                Next payout:{" "}
                {stokvel.currentCycle?.nextPayoutDate
                  ? new Date(
                      stokvel.currentCycle.nextPayoutDate,
                    ).toLocaleDateString()
                  : "Not scheduled"}
              </p>
              <Link
                href={`/stokvels/${stokvel.stokvelId}`}
                className="inline-block text-blue-700 underline"
              >
                View stokvel
              </Link>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

export default function MemberDashboardPage() {
  return (
    <Suspense fallback={<p>Loading your data...</p>}>
      <MemberDashboardPageContent />
    </Suspense>
  );
}