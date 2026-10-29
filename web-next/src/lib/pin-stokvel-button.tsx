"use client";

import { usePinnedStokvels } from "@/lib/pinned-stokvels-provider";

type PinStokvelButtonProps = {
  id: string;
  name: string;
};

export function PinStokvelButton({
  id,
  name,
}: PinStokvelButtonProps) {
  const isPinned = usePinnedStokvels((state) =>
    state.pins.some(
      (item) => item.id === id,
    ),
  );

  const pin = usePinnedStokvels(
    (state) => state.pin,
  );

  const unpin = usePinnedStokvels(
    (state) => state.unpin,
  );

  return (
    <button
      type="button"
      aria-pressed={isPinned}
      onClick={() => {
        if (isPinned) {
          unpin(id);
        } else {
          pin({ id, name });
        }
      }}
      className={
        "rounded-md border px-4 py-2 text-sm font-semibold " +
        (isPinned
          ? "border-gray-300 bg-gray-100 text-gray-900"
          : "border-blue-700 bg-white text-blue-700")
      }
    >
      {isPinned ? "Unpin" : "Pin"}
    </button>
  );
}
