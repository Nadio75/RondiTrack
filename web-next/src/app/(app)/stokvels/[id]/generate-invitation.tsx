"use client";

import { useState, useTransition } from "react";
import { createInvitationAction } from "./invitation-actions";

export function GenerateInvitation({
  stokvelId,
}: {
  stokvelId: string;
}) {
  const [pending, startTransition] = useTransition();
  const [code, setCode] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  function generate() {
    setError(null);

    startTransition(async () => {
      const result = await createInvitationAction(stokvelId);

      if (!result.success) {
        setError(
          result.correlationId
            ? `${result.error} Correlation ID: ${result.correlationId}`
            : result.error,
        );
        return;
      }

      setCode(result.code);
    });
  }

  async function copyLink() {
    if (!code) return;

    const link = `${window.location.origin}/join?code=${encodeURIComponent(code)}&step=rules`;

    try {
      await navigator.clipboard.writeText(link);
    } catch {
      setError("Unable to copy automatically. Please copy the link manually.");
    }
  }

  const inviteLink = code
    ? `/join?code=${encodeURIComponent(code)}&step=rules`
    : null;

  return (
    <section className="rounded-xl border bg-white p-6">
      <h2 className="mb-3 text-xl font-semibold">
        Invite someone to this stokvel
      </h2>

      <p className="mb-4 text-sm text-gray-600">
        Generate a private invitation link to share with a new member.
        Invitations expire after seven days.
      </p>

      <button
        type="button"
        onClick={generate}
        disabled={pending}
        className="rounded-lg bg-blue-700 px-4 py-2 font-medium text-white disabled:opacity-50"
      >
        {pending ? "Generating..." : "Generate Invitation"}
      </button>

      {code && inviteLink && (
        <div className="mt-4 space-y-3">
          <p className="font-medium">Invitation created successfully.</p>
          <p className="break-all rounded border bg-gray-50 p-3 text-sm">
            {code}
          </p>
          <a
            href={inviteLink}
            className="block break-all text-sm text-blue-700 underline"
          >
            Open invitation link
          </a>
          <button
            type="button"
            onClick={copyLink}
            className="rounded-lg border px-4 py-2 hover:bg-gray-50"
          >
            Copy invitation link
          </button>
        </div>
      )}

      {error && (
        <p role="alert" className="mt-3 text-sm text-red-700">
          {error}
        </p>
      )}
    </section>
  );
}