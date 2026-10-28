"use client";

import { useActionState } from "react";
import {
  recordContribution,
  type ContributionActionState,
} from "./actions";

const initialState: ContributionActionState = {
  status: "idle",
  message: "",
};

type Props = {
  stokvelId: string;
  cycleId: string;
};

export function ContributionForm({ stokvelId, cycleId }: Props) {
  const [state, formAction, isPending] = useActionState(
    recordContribution.bind(null, stokvelId, cycleId),
    initialState,
  );

  return (
    <form action={formAction} className="space-y-4 rounded-lg border p-4">
      <h3 className="text-lg font-semibold">Record your contribution</h3>

      <div>
        <label htmlFor="contribution-amount" className="block text-sm font-medium">
          Amount (R)
        </label>

        <input
          id="contribution-amount"
          name="amount"
          type="number"
          min="0.01"
          step="0.01"
          required
          disabled={isPending}
          placeholder="500.00"
          className="mt-1 w-full rounded-md border px-3 py-2"
        />
      </div>

      {state.message && (
        <p
          role="status"
          aria-live="polite"
          className={state.status === "error" ? "text-red-600" : "text-green-700"}
        >
          {state.message}
          {state.correlationId && (
            <span className="block text-xs">
              Reference: {state.correlationId}
            </span>
          )}
        </p>
      )}

      <button
        type="submit"
        disabled={isPending}
        className="rounded-md bg-blue-700 px-4 py-2 font-medium text-white disabled:cursor-not-allowed disabled:opacity-50"
      >
        {isPending ? "Recording..." : "Record contribution"}
      </button>
    </form>
  );
}
