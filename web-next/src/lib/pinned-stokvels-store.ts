import { createStore } from "zustand/vanilla";
import {
  createJSONStorage,
  persist,
} from "zustand/middleware";

export type PinnedStokvel = {
  id: string;
  name: string;
};

export type PinnedStokvelState = {
  pins: PinnedStokvel[];
  pin: (stokvel: PinnedStokvel) => void;
  unpin: (id: string) => void;
  clearPins: () => void;
};

export const pinnedStokvelStorageKey =
  "ronditrack-pinned-stokvels";

export function createPinnedStokvelStore() {
  return createStore<PinnedStokvelState>()(
    persist(
      (set) => ({
        pins: [],

        pin: (stokvel) =>
          set((state) => {
            const alreadyPinned = state.pins.some(
              (item) => item.id === stokvel.id,
            );

            if (alreadyPinned) {
              return state;
            }

            return {
              pins: [...state.pins, {
                id: stokvel.id,
                name: stokvel.name,
              }],
            };
          }),

        unpin: (id) =>
          set((state) => ({
            pins: state.pins.filter(
              (item) => item.id !== id,
            ),
          })),

        clearPins: () => set({ pins: [] }),
      }),
      {
        name: pinnedStokvelStorageKey,

        storage: createJSONStorage(
          () => localStorage,
        ),

        partialize: (state) => ({
          pins: state.pins.map((item) => ({
            id: item.id,
            name: item.name,
          })),
        }),

        skipHydration: true,
      },
    ),
  );
}

export type PinnedStokvelStore = ReturnType<
  typeof createPinnedStokvelStore
>;

export const pinnedStokvelOwnerStorageKey =
  "ronditrack-pinned-stokvels-owner";

export function preparePinnedStokvelStorageForUser(
  userId: string,
): void {
  if (!userId.trim()) {
    throw new Error("A valid user ID is required.");
  }

  const previousOwner = localStorage.getItem(
    pinnedStokvelOwnerStorageKey,
  );

  if (previousOwner !== userId) {
    // An unknown or different owner must never inherit
    // pins left behind by another account.
    localStorage.removeItem(pinnedStokvelStorageKey);
  }

  localStorage.setItem(
    pinnedStokvelOwnerStorageKey,
    userId,
  );
}