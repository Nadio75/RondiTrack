import { Suspense } from "react";
import Link from "next/link";
import { apiRequest } from "@/lib/api";
import { ActivityFeed } from "./activity-feed";
import type {
  ActivityFeedItem,
  PagedResponse,
} from "@/lib/types";

async function ActivityContent() {
  const firstPage = await apiRequest<
    PagedResponse<ActivityFeedItem>
  >("/api/v1/activity?pageSize=20");

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <Link
          href="/stokvels"
          className="text-sm text-blue-700 hover:underline"
        >
          Back to my stokvels
        </Link>

        <h1 className="mt-3 text-3xl font-bold">
          My Activity
        </h1>

        <p className="mt-2 text-gray-600">
          Your contributions and payouts across your stokvels,
          newest first.
        </p>
      </div>

      <ActivityFeed initialPage={firstPage} />
    </div>
  );
}

export default function ActivityPage() {
  return (
    <Suspense fallback={<p>Loading your activity...</p>}>
      <ActivityContent />
    </Suspense>
  );
}
