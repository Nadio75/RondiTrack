"use client";

import { useActionState, useEffect, useRef, useState } from "react";
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
  const [amount, setAmount] = useState("");
  const [state, formAction, isPending] = useActionState(
    recordContribution.bind(null, stokvelId, cycleId),
    initialState,
  );

  const previousStatus = useRef(state.status);

  useEffect(() => {
    if (
      state.status === "success" &&
      previousStatus.current !== "success"
    ) {
      setAmount("");
    }

    previousStatus.current = state.status;
  }, [state.status]);

  const amountError = state.fieldErrors?.amount;
  const hasError = state.status === "error";

  return (
    <form
      action={formAction}
      className="space-y-4 rounded-lg border p-4"
    >
      <h3 className="text-lg font-semibold">
        Record your contribution
      </h3>

      <div>
        <label
          htmlFor="contribution-amount"
          className="block text-sm font-medium"
        >
          Amount (R)
        </label>

        <input
          id="contribution-amount"
          name="amount"
          type="text"
          inputMode="decimal"
          required
          value={amount}
          onChange={(event) => setAmount(event.target.value)}
          disabled={isPending}
          aria-invalid={Boolean(amountError)}
          aria-describedby={
            amountError ? "contribution-amount-error" : undefined
          }
          placeholder="500.00"
          className="mt-1 w-full rounded-md border px-3 py-2"
        />

        {amountError && (
          <p
            id="contribution-amount-error"
            className="mt-1 text-sm text-red-600"
          >
            {amountError}
          </p>
        )}
      </div>

      {state.message && (
        <p
          role={hasError ? "alert" : "status"}
          aria-live={hasError ? "assertive" : "polite"}
          className={
            hasError ? "text-red-600" : "text-green-700"
          }
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
