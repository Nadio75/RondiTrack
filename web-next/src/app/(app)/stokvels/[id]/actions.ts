"use server";

import { randomUUID } from "node:crypto";
import { revalidatePath } from "next/cache";
import { ApiError, apiRequest } from "@/lib/api";
import { getSession } from "@/lib/session";
import { contributionSchema, toCents } from "@/lib/contribution-validation";

export type ContributionActionState = {
  status: "idle" | "success" | "error";
  message: string;
  correlationId?: string;
  retryKey?: string;
  retryFingerprint?: string;
};


const guidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export async function recordContribution(
  stokvelId: string,
  cycleId: string,
  _previousState: ContributionActionState,
  formData: FormData,
): Promise<ContributionActionState> {
  if (!guidPattern.test(stokvelId) || !guidPattern.test(cycleId)) {
    return {
      status: "error",
      message: "Invalid stokvel or contribution cycle.",
    };
  }

  const parsed = contributionSchema.safeParse({
    amount: formData.get("amount"),
  });

  if (!parsed.success) {
    return {
      status: "error",
      message: parsed.error.issues[0]?.message ?? "Invalid amount.",
    };
  }

  const session = await getSession();

  if (!session) {
    return {
      status: "error",
      message: "Your session has expired. Please log in again.",
    };
  }

  const amountInCents = toCents(parsed.data.amount);

  const fingerprint = [
    stokvelId.toLowerCase(),
    cycleId.toLowerCase(),
    session.user.id.toLowerCase(),
    amountInCents,
  ].join(":");

  const idempotencyKey =
    _previousState.status === "error" &&
    _previousState.retryFingerprint === fingerprint &&
    _previousState.retryKey
      ? _previousState.retryKey
      : randomUUID();

  try {
    await apiRequest<unknown>(
      `/api/v1/stokvels/${encodeURIComponent(stokvelId)}` +
        `/cycles/${encodeURIComponent(cycleId)}/contributions`,
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify({
          userId: session.user.id,
          amountInCents,
        }),
      },
    );
  } catch (error) {
    if (error instanceof ApiError) {
      let message: string;

      switch (error.status) {
        case 400:
        case 422:
          message = error.message;
          break;
        case 403:
          message = "You are not authorized to record this contribution.";
          break;
        case 404:
          message = "The stokvel or contribution cycle was not found.";
          break;
        case 409:
          message = "This contribution conflicts with an existing payment.";
          break;
        case 429:
          message = error.retryAfter !== null
            ? `Too many requests. Try again in ${error.retryAfter} seconds.`
            : "Too many requests. Please try again later.";
          break;
        default:
          message = "Unable to record the contribution. Please try again.";
      }

      return {
        status: "error",
        message,
        correlationId: error.correlationId ?? undefined,
        retryKey: idempotencyKey,
        retryFingerprint: fingerprint,
      };
    }

    return {
      status: "error",
      message: "A network or server error occurred. Please try again.",
      retryKey: idempotencyKey,
      retryFingerprint: fingerprint,
    };
  }

  revalidatePath(`/stokvels/${stokvelId}`);
  revalidatePath("/dashboard/member");
  revalidatePath("/dashboard/treasurer");

  return {
    status: "success",
    message: "Contribution recorded successfully.",
  };
}


