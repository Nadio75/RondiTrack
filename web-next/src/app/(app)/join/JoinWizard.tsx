"use client";

import { useEffect, useReducer, useState, useTransition } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { previewInvitationAction, joinStokvelAction } from "./actions";
import {
  joinWizardReducer,
  type JoinWizardState,
} from "./join-wizard-state";

const initialState: JoinWizardState = {
  step: "code",
  code: "",
};

type JoinWizardProps = {
  initialCode: string;

};

export default function JoinWizard({
  initialCode,

}: JoinWizardProps) {
  const [state, dispatch] = useReducer(joinWizardReducer, initialState);
  const [code, setCode] = useState(initialCode);
  const [error, setError] = useState<string | null>(null);
  const [correlationId, setCorrelationId] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();
  const router = useRouter();
  const searchParams = useSearchParams();
  const urlCode = searchParams.get("code") ?? "";
  const urlStep = searchParams.get("step") ?? "";
  const currentCode = state.step === "success" ? "" : state.code;
  const hasInvitation =
    state.step === "rules" || state.step === "confirm";

  useEffect(() => {
    const invitationCode = urlCode.trim();

    if (!invitationCode) return;

    let cancelled = false;

    async function loadInvitation() {
      const result = await previewInvitationAction(invitationCode);

      if (cancelled) return;

      if (!result.success) {
        setError(result.error);
        setCorrelationId(result.correlationId);
        return;
      }

      setError(null);
      setCorrelationId(null);
      setCode(invitationCode);

      dispatch({
        type: "PREVIEW_LOADED",
        code: invitationCode,
        invitation: result.invitation,
      });


    }

    void loadInvitation();

    return () => {
      cancelled = true;
    };
  }, [urlCode]);

  useEffect(() => {
    if (state.step === "success") return;

    if (!urlCode) {
      if (state.step !== "code") {
        dispatch({ type: "NAVIGATE", step: "code" });
      }
      return;
    }

    if (!hasInvitation || currentCode !== urlCode.trim()) return;

    const requestedStep = urlStep === "confirm" ? "confirm" : "rules";

    if (state.step !== requestedStep) {
      dispatch({ type: "NAVIGATE", step: requestedStep });
    }
  }, [urlCode, urlStep, currentCode, hasInvitation, state.step]);
  function navigateToStep(
    step: "code" | "rules" | "confirm",
    invitationCode: string,
  ) {
    const params = new URLSearchParams();

    if (invitationCode) {
      params.set("code", invitationCode);
    }

    if (step !== "code") {
      params.set("step", step);
    }

    const query = params.toString();

    router.push(query ? `/join?${query}` : "/join");
  }

  function handlePreview(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const invitationCode = code.trim();

    if (!invitationCode) {
      setError("Please enter an invitation code.");
      return;
    }

    setError(null);
    setCorrelationId(null);

    startTransition(async () => {
      const result = await previewInvitationAction(invitationCode);

      if (!result.success) {
        setError(result.error);
        setCorrelationId(result.correlationId);
        return;
      }

      dispatch({
        type: "PREVIEW_LOADED",
        code: invitationCode,
        invitation: result.invitation,
      });

      navigateToStep("rules", invitationCode);
    });
  }

  function handleJoin() {
    if (state.step !== "confirm" || pending) return;

    const invitationCode = state.code;

    setError(null);
    setCorrelationId(null);

    startTransition(async () => {
      const result = await joinStokvelAction(invitationCode);

      if (!result.success) {
        setError(result.error);
        setCorrelationId(result.correlationId);
        return;
      }

      dispatch({ type: "JOINED", stokvelId: result.stokvelId });
      router.replace("/join");
    });
  }

  return (
    <section className="mx-auto max-w-2xl space-y-6">
      <header>
        <h1 className="text-3xl font-bold">Join a stokvel</h1>
        <p className="mt-2 text-gray-600">
          Enter your invitation code and review the stokvel details.
        </p>
      </header>

      {state.step === "code" && (
        <form onSubmit={handlePreview} className="space-y-4 rounded-xl border bg-white p-6">
          <label htmlFor="invitation-code" className="block font-medium">
            Invitation code
          </label>

          <input
            id="invitation-code"
            value={code}
            onChange={(event) => setCode(event.target.value)}
            autoComplete="off"
            required
            maxLength={128}
            className="w-full rounded-md border p-3"
            placeholder="Paste your invitation code"
          />

          <button
            type="submit"
            disabled={pending}
            className="rounded-md bg-blue-700 px-5 py-3 font-semibold text-white disabled:opacity-50"
          >
            {pending ? "Checking invitation..." : "Continue"}
          </button>
        </form>
      )}

      {state.step === "rules" && (
        <div className="space-y-5 rounded-xl border bg-white p-6">
          <h2 className="text-2xl font-semibold">{state.invitation.name}</h2>

          <div>
            <h3 className="font-semibold">Stokvel rules</h3>
            <p className="mt-2 whitespace-pre-wrap text-gray-700">
              {state.invitation.rules || "No additional rules have been provided."}
            </p>
          </div>

          <dl className="space-y-3">
            <div>
              <dt className="font-semibold">Contribution amount</dt>
              <dd>R {state.invitation.contributionAmount.toFixed(2)}</dd>
            </div>

            <div>
              <dt className="font-semibold">Monthly closing day</dt>
              <dd>Day {state.invitation.closingDayOfMonth}</dd>
            </div>

            <div>
              <dt className="font-semibold">Number of members</dt>
              <dd>{state.invitation.memberCount}</dd>
            </div>
          </dl>

          <div className="flex gap-3">
            <button
              type="button"
              onClick={() => {
                setCode(state.code);
                dispatch({ type: "BACK" });
                navigateToStep("code", "");
              }}
              className="rounded-md border px-5 py-3"
            >
              Back
            </button>

            <button
              type="button"
              onClick={() => {
                dispatch({ type: "CONTINUE" });
                navigateToStep("confirm", state.code);
              }}
              className="rounded-md bg-blue-700 px-5 py-3 text-white"
            >
              Continue to confirmation
            </button>
          </div>
        </div>
      )}

      {state.step === "confirm" && (
        <div className="space-y-4 rounded-xl border bg-white p-6">
          <h2 className="text-2xl font-semibold">Confirm your membership</h2>

          <p>
            You are about to join <strong>{state.invitation.name}</strong>.
          </p>

          <p>
            By continuing, you confirm that you have reviewed the stokvel rules.
          </p>

          <button
            type="button"
            onClick={() => {
              dispatch({ type: "BACK" });
              navigateToStep("rules", state.code);
            }}
            className="rounded-md border px-5 py-3"
          >
            Back to rules
          </button>

          <button
            type="button"
            onClick={handleJoin}
            disabled={pending}
            className="rounded-md bg-blue-700 px-5 py-3 font-semibold text-white disabled:opacity-50"
          >
            {pending ? "Joining..." : "Join Stokvel"}
          </button>
        </div>
      )}

      {state.step === "success" && (
        <div role="status" className="space-y-4 rounded-xl border bg-white p-6">
          <h2 className="text-2xl font-semibold">Membership confirmed!</h2>
          <p>You have successfully joined the stokvel.</p>
          <a
            href={`/stokvels/${state.stokvelId}`}
            className="inline-block rounded-md bg-blue-700 px-5 py-3 text-white"
          >
            View your stokvel
          </a>
        </div>
      )}
      {error && (
        <div role="alert" className="rounded-md border border-red-300 bg-red-50 p-4 text-red-700">
          <p>{error}</p>
          {correlationId && <p>Correlation ID: {correlationId}</p>}
        </div>
      )}
    </section>
  );
}
