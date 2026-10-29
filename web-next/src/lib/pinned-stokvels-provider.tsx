"use client";

import {
  createContext,
  useContext,
  useEffect,
  useState,
} from "react";
import { useStore } from "zustand";
import {
  createPinnedStokvelStore,
  preparePinnedStokvelStorageForUser,
  type PinnedStokvelState,
  type PinnedStokvelStore,
} from "@/lib/pinned-stokvels-store";

const PinnedStokvelContext =
  createContext<PinnedStokvelStore | null>(null);

export function PinnedStokvelProvider({
  children,
  userId,
}: {
  children: React.ReactNode;
  userId: string;
}) {
  const [store] = useState(
    () => createPinnedStokvelStore(),
  );

  useEffect(() => {
    // Check ownership before restoring persisted pins.
    preparePinnedStokvelStorageForUser(userId);

    void store.persist.rehydrate();
  }, [store, userId]);

  return (
    <PinnedStokvelContext.Provider value={store}>
      {children}
    </PinnedStokvelContext.Provider>
  );
}

export function usePinnedStokvels<T>(
  selector: (state: PinnedStokvelState) => T,
): T {
  const store = useContext(PinnedStokvelContext);

  if (!store) {
    throw new Error(
      "usePinnedStokvels must be used inside PinnedStokvelProvider.",
    );
  }

  return useStore(store, selector);
}

export function usePinnedStokvelStoreApi() {
  const store = useContext(PinnedStokvelContext);

  if (!store) {
    throw new Error(
      "PinnedStokvelProvider is missing.",
    );
  }

  return store;
}
