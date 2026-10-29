"use client";

import { usePinnedStokvelStoreApi } from "@/lib/pinned-stokvels-provider";
import {
  pinnedStokvelStorageKey,
  pinnedStokvelOwnerStorageKey,
} from "@/lib/pinned-stokvels-store";

export function SecureLogoutButton() {
  const store = usePinnedStokvelStoreApi();

  return (
    <button
      type="submit"
      onClick={() => {
        store.getState().clearPins();

        // Remove saved pins even if the store has
        // not finished hydrating from localStorage.
        localStorage.removeItem(
          pinnedStokvelStorageKey,
        );
        localStorage.removeItem(
          pinnedStokvelOwnerStorageKey,
        );
      }}
      className="rounded-md bg-slate-900 px-4 py-2 font-semibold text-white"
    >
      Sign out
    </button>
  );
}
