import { notFound } from "next/navigation";
import { apiRequest, ApiError } from "@/lib/api";
import type {
  HomeDashboard,
  StokvelDetail,
} from "@/lib/types";

export default async function StokvelDetailPage({
  params,
}: PageProps<"/stokvels/[id]">) {
  const { id } = await params;

  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)) {
    notFound();
  }

  const home = await apiRequest<HomeDashboard>("/api/v1/me/home");

  const membership = home.stokvels.find(
    (stokvel) => stokvel.stokvelId.toLowerCase() === id.toLowerCase(),
  );

  if (!membership) {
    notFound();
  }

  let stokvel: StokvelDetail;

  try {
    stokvel = await apiRequest<StokvelDetail>(
      `/api/v1/stokvels/${encodeURIComponent(id)}`,
    );
  } catch (error) {
    if (
      error instanceof ApiError &&
      (error.status === 403 || error.status === 404)
    ) {
      notFound();
    }

    throw error;
  }

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
    </section>
  );
}
