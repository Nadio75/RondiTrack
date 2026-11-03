"use server";

import { revalidatePath } from "next/cache";
import { ApiError, apiBaseUrl, createApiError } from "@/lib/api";
import { getSession } from "@/lib/session";

export type ProofActionState = {
  status: "idle" | "success" | "error";
  message: string;
  correlationId?: string;
};

const guidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const maxBytes = 5 * 1024 * 1024;

function messageForError(error: unknown): ProofActionState {
  if (error instanceof ApiError) {
    let message: string;

    switch (error.status) {
      case 400:
      case 413:
      case 422:
        message = error.message;
        break;
      case 403:
        message = "You are not allowed to manage this receipt.";
        break;
      case 404:
        message = "This receipt or contribution was not found.";
        break;
      case 409:
        message = "This cycle is closed. Receipts can no longer be changed.";
        break;
      case 429:
        message = error.retryAfter !== null
          ? `Too many requests. Try again in ${error.retryAfter} seconds.`
          : "Too many requests. Please try again later.";
        break;
      default:
        message = "The receipt could not be saved. Please try again.";
    }

    return {
      status: "error",
      message,
      correlationId: error.correlationId ?? undefined,
    };
  }

  return {
    status: "error",
    message: "A network or server error occurred. Please try again.",
  };
}

function proofUrl(stokvelId: string, contributionId: string) {
  return `/api/v1/stokvels/${encodeURIComponent(stokvelId)}` +
    `/contributions/${encodeURIComponent(contributionId)}/proof`;
}

export async function uploadProof(
  stokvelId: string,
  contributionId: string,
  _previous: ProofActionState,
  formData: FormData,
): Promise<ProofActionState> {
  if (!guidPattern.test(stokvelId) || !guidPattern.test(contributionId)) {
    return { status: "error", message: "Invalid contribution." };
  }

  const file = formData.get("file");

  if (!(file instanceof File) || file.size === 0) {
    return { status: "error", message: "Please select a receipt." };
  }

  if (file.size > maxBytes) {
    return { status: "error", message: "Receipt must be 5 MB or smaller." };
  }

  if (!["application/pdf", "image/jpeg", "image/png"].includes(file.type)) {
    return { status: "error", message: "Only PDF, JPG and PNG files are allowed." };
  }

  const session = await getSession();

  if (!session) {
    return { status: "error", message: "Please log in again to upload." };
  }

  const upload = new FormData();
  upload.append("file", file, file.name);

  try {
    const response = await fetch(
      `${apiBaseUrl}${proofUrl(stokvelId, contributionId)}`,
      {
        method: "POST",
        headers: {
          Authorization: `Bearer ${session.accessToken}`,
          Accept: "application/json",
        },
        body: upload,
        cache: "no-store",
      },
    );

    if (!response.ok) {
      throw await createApiError(response);
    }

    revalidatePath(`/stokvels/${stokvelId}`);
    revalidatePath(`/stokvels/${stokvelId}/contributions`);

    return {
      status: "success",
      message: "Receipt uploaded successfully.",
    };
  } catch (error) {
    return messageForError(error);
  }
}

export async function deleteProof(
  stokvelId: string,
  contributionId: string,
): Promise<ProofActionState> {
  if (!guidPattern.test(stokvelId) || !guidPattern.test(contributionId)) {
    return { status: "error", message: "Invalid contribution." };
  }

  const session = await getSession();

  if (!session) {
    return { status: "error", message: "Please log in again." };
  }

  try {
    const response = await fetch(
      `${apiBaseUrl}${proofUrl(stokvelId, contributionId)}`,
      {
        method: "DELETE",
        headers: {
          Authorization: `Bearer ${session.accessToken}`,
        },
        cache: "no-store",
      },
    );

    if (!response.ok) {
      throw await createApiError(response);
    }

    revalidatePath(`/stokvels/${stokvelId}`);
    revalidatePath(`/stokvels/${stokvelId}/contributions`);

    return {
      status: "success",
      message: "Receipt deleted successfully.",
    };
  } catch (error) {
    return messageForError(error);
  }
}
