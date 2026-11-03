"use server";

import { apiRequest } from "@/lib/api";
import { revalidatePath } from "next/cache";

export type ClosingPreview = {
  stokvelId: string;
  cycleId: string;
  period: string;
  closingAtUtc: string;
  version: number;
  targetAmountCents: number;
  totalArrearsCents: number;
  arrears: {
    userId: string;
    memberName: string;
    paidCents: number;
    arrearsCents: number;
  }[];
  snapshotToken: string;
  isDue: boolean;
};

export type ClosingActionResult =
  | { ok: true; message: string; replayed?: boolean }
  | { ok: false; status: number; message: string };

export async function getClosingPreview(
  stokvelId: string,
  cycleId: string,
): Promise<ClosingPreview> {
  return apiRequest<ClosingPreview>(
    `/api/v1/stokvels/${stokvelId}/cycles/${cycleId}/closing-preview`,
  );
}

export async function confirmCycleClosing(
  stokvelId: string,
  cycleId: string,
  snapshotToken: string,
  idempotencyKey: string,
): Promise<ClosingActionResult> {
  try {
    const result = await apiRequest<{ replayed: boolean }>(
      `/api/v1/stokvels/${stokvelId}/cycles/${cycleId}/close`,
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify({ snapshotToken }),
      },
    );

    revalidatePath(`/stokvels/${stokvelId}`);
    revalidatePath(`/stokvels/${stokvelId}/contributions`);

    return {
      ok: true,
      message: result.replayed
        ? "This cycle was already closed successfully."
        : "Cycle closed successfully.",
      replayed: result.replayed,
    };
  } catch (error) {
    const status =
      typeof error === "object" &&
      error !== null &&
      "status" in error &&
      typeof error.status === "number"
        ? error.status
        : 500;

    return {
      ok: false,
      status,
      message:
        status === 409
          ? "The closing information has changed. Refresh the preview before confirming again."
          : status === 403
            ? "Only the Treasurer can close this cycle."
            : "Closing failed. Please try again.",
    };
  }
}
