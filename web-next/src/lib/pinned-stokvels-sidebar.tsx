"use client";

import Link from "next/link";
import { usePinnedStokvels } from "@/lib/pinned-stokvels-provider";

export function PinnedStokvelsSidebar() {
  const pins = usePinnedStokvels(
    (state) => state.pins,
  );

  if (pins.length === 0) {
    return null;
  }

  return (
    <section aria-label="Pinned stokvels">
      <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide">
        Pinned Stokvels
      </h2>

      <ul className="space-y-2">
        {pins.map((stokvel) => (
          <li key={stokvel.id}>
            <Link
              href={`/stokvels/${encodeURIComponent(stokvel.id)}`}
              className="block rounded-md px-3 py-2 text-sm hover:bg-gray-100"
            >
              <span aria-hidden="true">📌 </span>
              {stokvel.name}
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
