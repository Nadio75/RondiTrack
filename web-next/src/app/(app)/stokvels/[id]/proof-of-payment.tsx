"use client";

import {
  useActionState,
  useEffect,
  useRef,
  useState,
  useTransition,
} from "react";
import {
  uploadProof,
  deleteProof,
  type ProofActionState,
} from "./proof-actions";

const initialState: ProofActionState = {
  status: "idle",
  message: "",
};

const maxBytes = 5 * 1024 * 1024;

type ReceiptStatus = {
  hasReceipt: boolean;
  fileName: string | null;
  uploadedAtUtc: string | null;
  isClosed: boolean;
};

type Props = {
  stokvelId: string;
  contributionId: string;
  isClosed: boolean;
};

export function ProofOfPayment({
  stokvelId,
  contributionId,
  isClosed,
}: Props) {

  const [receipt, setReceipt] = useState<ReceiptStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [statusError, setStatusError] = useState("");
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [validationError, setValidationError] = useState("");
  const [deleteResult, setDeleteResult] = useState<ProofActionState | null>(null);
  const [deleting, startDelete] = useTransition();
  const [feedbackVersion, setFeedbackVersion] = useState(0);
  const [uploadFeedbackVersion, setUploadFeedbackVersion] = useState(-1);
  const inputRef = useRef<HTMLInputElement>(null);

  const [state, formAction, uploading] = useActionState(
    async (previous: ProofActionState, formData: FormData) => {
      const result = await uploadProof(
        stokvelId,
        contributionId,
        previous,
        formData,
      );

      if (result.status === "success") {
        setFeedbackVersion((value) => value + 1);
        setUploadFeedbackVersion(0);
        setSelectedFile(null);
        if (inputRef.current) inputRef.current.value = "";
      }

      return result;
    },
    initialState,
  );

  const baseUrl =
    `/api/stokvels/${encodeURIComponent(stokvelId)}` +
    `/contributions/${encodeURIComponent(contributionId)}/proof`;

  const downloadUrl = baseUrl;
  const statusUrl = `${baseUrl}/status`;

  useEffect(() => {
    let cancelled = false;

    async function loadStatus() {
      try {
        const response = await fetch(statusUrl, {
          cache: "no-store",
        });

        if (!response.ok) {
          throw new Error("Could not load receipt status.");
        }

        const data = (await response.json()) as ReceiptStatus;

        if (!cancelled) {
          setReceipt(data);
          setStatusError("");
        }
      } catch {
        if (!cancelled) {
          setStatusError("Could not check whether a receipt exists.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void loadStatus();

    return () => {
      cancelled = true;
    };
  }, [statusUrl, feedbackVersion]);



  function validateFile(file: File | null) {
    setSelectedFile(file);
    setValidationError("");
    setDeleteResult(null);
    setUploadFeedbackVersion(-1);

    if (!file) return;

    if (file.size === 0) {
      setValidationError("The selected file is empty.");
    } else if (file.size > maxBytes) {
      setValidationError("Receipt must be 5 MB or smaller.");
    } else if (
      !["application/pdf", "image/jpeg", "image/png"].includes(file.type)
    ) {
      setValidationError("Only PDF, JPG and PNG files are allowed.");
    }
  }

  function handleDelete() {
    if (!window.confirm("Delete this payment receipt?")) return;

    startDelete(async () => {
      const result = await deleteProof(stokvelId, contributionId);
      setDeleteResult(result);
      setUploadFeedbackVersion(-1);

      if (result.status === "success") {
        setFeedbackVersion((value) => value + 1);
        setSelectedFile(null);
        if (inputRef.current) inputRef.current.value = "";
      }
    });
  }

  const closed = isClosed || receipt?.isClosed === true;

  return (
    <section className="space-y-3 rounded-lg border p-4">
      <h3 className="text-lg font-semibold">Proof of payment</h3>

      {loading && <p className="text-sm">Checking receipt...</p>}

      {statusError && (
        <p role="alert" className="text-sm text-red-700">
          {statusError}
        </p>
      )}

      {!loading && receipt?.hasReceipt && (
        <div className="space-y-2">
          <p className="text-sm">
            Receipt: <strong>{receipt.fileName}</strong>
          </p>
          <a
            href={downloadUrl}
            className="inline-block text-sm text-blue-700 underline"
          >
            Download receipt
          </a>
        </div>
      )}

      {closed ? (
        <p className="text-sm text-gray-600">
          This cycle is closed. Receipts cannot be changed.
        </p>
      ) : (
        !loading &&
        receipt &&
        !statusError && (
          <>
            <form action={formAction} className="space-y-3">
              <div>
                <label
                  htmlFor={`receipt-${contributionId}`}
                  className="block text-sm font-medium"
                >
                  {receipt.hasReceipt ? "Replace receipt" : "Upload receipt"}
                  {" — PDF, JPG or PNG (maximum 5 MB)"}
                </label>

                <input
                  ref={inputRef}
                  id={`receipt-${contributionId}`}
                  type="file"
                  name="file"
                  accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
                  required
                  disabled={uploading || deleting}
                  onChange={(event) =>
                    validateFile(event.target.files?.[0] ?? null)
                  }
                  className="mt-2 block w-full rounded-md border p-2 text-sm"
                />
              </div>

              {validationError && (
                <p role="alert" className="text-sm text-red-700">
                  {validationError}
                </p>
              )}

              {state.message &&
                !validationError &&
                !selectedFile &&
                uploadFeedbackVersion >= 0 && (
                  <p
                    role="status"
                    aria-live="polite"
                    className={
                      state.status === "error"
                        ? "text-sm text-red-700"
                        : "text-sm text-green-700"
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
                disabled={
                  !selectedFile ||
                  !!validationError ||
                  uploading ||
                  deleting
                }
                className="rounded-md bg-blue-700 px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
              >
                {uploading
                  ? "Saving..."
                  : receipt.hasReceipt
                    ? "Replace receipt"
                    : "Upload receipt"}
              </button>
            </form>

            {receipt.hasReceipt && (
              <button
                type="button"
                onClick={handleDelete}
                disabled={uploading || deleting}
                className="text-sm text-red-700 underline disabled:opacity-50"
              >
                {deleting ? "Deleting..." : "Delete receipt"}
              </button>
            )}

            {deleteResult?.message && (
              <p
                role="status"
                aria-live="polite"
                className={
                  deleteResult.status === "error"
                    ? "text-sm text-red-700"
                    : "text-sm text-green-700"
                }
              >
                {deleteResult.message}
                {deleteResult.correlationId && (
                  <span className="block text-xs">
                    Reference: {deleteResult.correlationId}
                  </span>
                )}
              </p>
            )}
          </>
        )
      )}
    </section>
  );
}
