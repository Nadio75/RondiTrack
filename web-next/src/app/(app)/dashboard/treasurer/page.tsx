import Link from "next/link";
import { apiRequest } from "@/lib/api";
import { stokvelsWhereTreasurer } from "@/lib/treasurer";
import type { HomeDashboard } from "@/lib/types";

export default async function TreasurerDashboardPage() {
  const home = await apiRequest<HomeDashboard>("/api/v1/me/home");
  const treasurerStokvels = stokvelsWhereTreasurer(home.stokvels);

  return (
    <section className="space-y-6">
      <h1 className="text-3xl font-bold">Treasurer Dashboard</h1>

      {treasurerStokvels.length === 0 ? (
        <div className="rounded-xl border bg-white p-6">
          <p>
            You are not currently a Treasurer in any stokvel.
          </p>
          <Link
            href="/dashboard/member"
            className="mt-3 inline-block text-blue-700 underline"
          >
            Go to Member Dashboard
          </Link>
        </div>
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          {treasurerStokvels.map((stokvel) => (
            <article
              key={stokvel.stokvelId}
              className="space-y-2 rounded-xl border bg-white p-6"
            >
              <h2 className="text-xl font-semibold">{stokvel.name}</h2>
              <p>Role: {stokvel.role}</p>
              <p>Contribution: {stokvel.contributionAmount}</p>
              <p>
                Cycle: {stokvel.currentCycle?.period ?? "None"}
              </p>
              <p>
                Status: {stokvel.currentCycle?.status ?? "Not available"}
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
