import { Suspense } from "react";
import Link from "next/link";
import { getTypedHomeDashboard } from "@/lib/home";
import { PinStokvelButton } from "@/lib/pin-stokvel-button";


async function StokvelsPageContent() {
  const home = await getTypedHomeDashboard();

  return (
    <section className="space-y-6">
      <h1 className="text-3xl font-bold">My Stokvels</h1>

      {home.stokvels.length === 0 ? (
        <p>You are not a member of any stokvels yet.</p>
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          {home.stokvels.map((stokvel) => (
            <article
              key={stokvel.stokvelId}
              className="rounded-xl border bg-white p-6 shadow-sm"
            >
              <div className="flex flex-wrap items-start justify-between gap-3">
                <h2 className="text-xl font-semibold">
                  {stokvel.name}
                </h2>

                <PinStokvelButton
                  id={stokvel.stokvelId}
                  name={stokvel.name}
                />
              </div>

              <p className="mt-2 text-gray-600">
                Your role: {stokvel.role}
              </p>

              <p>
                Contribution: {stokvel.contributionAmount}
              </p>

              <Link
                href={`/stokvels/${stokvel.stokvelId}`}
                className="mt-4 inline-block font-semibold text-blue-700 underline"
              >
                View details
              </Link>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}


export default function StokvelsPage() {
  return (
    <Suspense fallback={<p>Loading your data...</p>}>
      <StokvelsPageContent />
    </Suspense>
  );
}