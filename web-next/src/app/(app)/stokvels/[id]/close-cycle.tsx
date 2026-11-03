"use client";

import {
  useEffect,
  useRef,
  useState,
  useTransition,
} from "react";
import {
  confirmCycleClosing,
  getClosingPreview,
  type ClosingPreview,
} from "./closing-actions";

type Props = {
  stokvelId: string;
  cycleId: string;
};

function money(cents: number): string {
  return new Intl.NumberFormat("en-ZA", {
    style: "currency",
    currency: "ZAR",
  }).format(cents / 100);
}

function friendlyPeriod(period: string): string {
  const [year, month] = period.split("-").map(Number);

  return new Intl.DateTimeFormat("en-ZA", {
    month: "long",
    year: "numeric",
    timeZone: "UTC",
  }).format(new Date(Date.UTC(year, month - 1, 1)));
}

export function CloseCycle({ stokvelId, cycleId }: Props) {
  const [open, setOpen] = useState(false);
  const [preview, setPreview] = useState<ClosingPreview | null>(null);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [pending, startTransition] = useTransition();

  const triggerRef = useRef<HTMLButtonElement>(null);
  const cancelRef = useRef<HTMLButtonElement>(null);
  const dialogRef = useRef<HTMLDivElement>(null);
  const requestKeyRef = useRef<string | null>(null);

  function restoreFocus() {
    window.setTimeout(() => triggerRef.current?.focus(), 0);
  }

  function dismiss() {
    if (pending) return;
    setOpen(false);
    setConfirmed(false);
    setError("");
    restoreFocus();
  }

  function loadPreview() {
    setError("");
    setPreview(null);
    setConfirmed(false);
    requestKeyRef.current = null;

    startTransition(async () => {
      try {
        const next = await getClosingPreview(stokvelId, cycleId);
        setPreview(next);
      } catch (err) {
        setError(
          err instanceof Error
            ? err.message
            : "Unable to load the closing preview.",
        );
      }
    });
  }

  function showDialog() {
    setOpen(true);
    setSuccess("");
    loadPreview();
  }

  useEffect(() => {
    if (!open) return;

    cancelRef.current?.focus();

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        if (!pending) {
          event.preventDefault();
          setOpen(false);
          setConfirmed(false);
          setError("");
          window.setTimeout(() => triggerRef.current?.focus(), 0);
        }
        return;
      }

      if (event.key !== "Tab") return;

      const focusable = Array.from(
        dialogRef.current?.querySelectorAll<HTMLElement>(
          'button:not([disabled]), input:not([disabled])',
        ) ?? [],
      );

      if (focusable.length === 0) return;

      const first = focusable[0];
      const last = focusable[focusable.length - 1];

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener("keydown", onKeyDown);

    return () => document.removeEventListener("keydown", onKeyDown);
  }, [open, pending]);

  function submit() {
    if (!preview || !preview.isDue || !confirmed || pending) return;

    if (!requestKeyRef.current) {
      requestKeyRef.current = crypto.randomUUID();
    }

    const key = requestKeyRef.current;
    setError("");

    startTransition(async () => {
      const result = await confirmCycleClosing(
        stokvelId,
        cycleId,
        preview.snapshotToken,
        key,
      );

      if (result.ok) {
        setSuccess(result.message);
        setOpen(false);
        setConfirmed(false);
        restoreFocus();
        window.location.reload();
        return;
      }

      if (result.status === 409) {
        setError(result.message);
        setConfirmed(false);
        setPreview(null);
        requestKeyRef.current = null;

        try {
          const next = await getClosingPreview(stokvelId, cycleId);
          setPreview(next);
        } catch {
          setError(
            "The cycle has changed or is already closed. Refresh the page.",
          );
        }

        return;
      }

      setError(result.message);
    });
  }

  return (
    <div className="space-y-3">
      <button
        ref={triggerRef}
        type="button"
        onClick={showDialog}
        className="rounded-lg bg-slate-900 px-4 py-2 font-medium text-white"
      >
        Close cycle
      </button>

      {success && (
        <p role="status" className="text-green-700">
          {success}
        </p>
      )}

      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
          <div
            ref={dialogRef}
            role="alertdialog"
            aria-modal="true"
            aria-labelledby="closing-title"
            aria-describedby="closing-description"
            className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-xl bg-white p-6 shadow-xl"
          >
            <h2
              id="closing-title"
              className="text-xl font-bold"
            >
              Confirm cycle closing
            </h2>

            {pending && !preview && (
              <p role="status" className="mt-4">
                Loading closing information...
              </p>
            )}

            {preview && (
              <div className="mt-4 space-y-4">
                <p
                  id="closing-description"
                  className="text-lg font-semibold"
                >
                  Close {friendlyPeriod(preview.period)} and record{" "}
                  {money(preview.totalArrearsCents)} in arrears?
                </p>

                <p className="text-sm text-slate-600">
                  Contribution expected per member:{" "}
                  {money(preview.targetAmountCents)}
                </p>

                {preview.arrears.length === 0 ? (
                  <p>No outstanding payments were found.</p>
                ) : (
                  <ul className="space-y-2">
                    {preview.arrears.map((member) => (
                      <li
                        key={member.userId}
                        className="flex justify-between gap-3 border-b pb-2"
                      >
                        <span>{member.memberName}</span>
                        <strong>{money(member.arrearsCents)}</strong>
                      </li>
                    ))}
                  </ul>
                )}

                {!preview.isDue && (
                  <p role="status" className="text-amber-800">
                    This cycle cannot be closed before its scheduled deadline.
                  </p>
                )}

                <label className="flex items-start gap-2">
                  <input
                    type="checkbox"
                    checked={confirmed}
                    disabled={!preview.isDue || pending}
                    onChange={(event) =>
                      setConfirmed(event.target.checked)
                    }
                    className="mt-1"
                  />
                  <span>
                    I have reviewed these amounts and confirm this closing.
                  </span>
                </label>
              </div>
            )}

            {error && (
              <p
                role="alert"
                className="mt-4 rounded-lg bg-red-50 p-3 text-red-800"
              >
                {error}
              </p>
            )}

            <div className="mt-6 flex justify-end gap-3">
              <button
                ref={cancelRef}
                type="button"
                onClick={dismiss}
                disabled={pending}
                className="rounded-lg border px-4 py-2"
              >
                Cancel
              </button>

              {preview && (
                <button
                  type="button"
                  onClick={submit}
                  disabled={!confirmed || !preview.isDue || pending}
                  className="rounded-lg bg-red-700 px-4 py-2 font-semibold text-white disabled:opacity-50"
                >
                  {pending ? "Closing..." : "Confirm closing"}
                </button>
              )}

              {!preview && !pending && (
                <button
                  type="button"
                  onClick={loadPreview}
                  className="rounded-lg border px-4 py-2"
                >
                  Retry preview
                </button>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

