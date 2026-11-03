"use server";

import { ApiError, apiRequest } from "@/lib/api";

export type InvitationCreationResult =
  | {
      success: true;
      code: string;
      expiresAtUtc: string;
    }
  | {
      success: false;
      error: string;
      correlationId: string | null;
    };

export async function createInvitationAction(
  stokvelId: string,
): Promise<InvitationCreationResult> {
  if (!/^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(stokvelId)) {
    return {
      success: false,
      error: "Invalid stokvel ID.",
      correlationId: null,
    };
  }

  try {
    const result = await apiRequest<{
      code: string;
      expiresAtUtc: string;
    }>(`/api/v1/stokvels/${stokvelId}/invites`, {
      method: "POST",
    });

    return {
      success: true,
      code: result.code,
      expiresAtUtc: result.expiresAtUtc,
    };
  } catch (error) {
    if (error instanceof ApiError) {
      const message =
        error.status === 403
          ? "Only a Chairperson or Treasurer can create invitations."
          : error.status === 429
            ? "Too many requests. Please try again later."
            : "Unable to create an invitation. Please try again.";

      return {
        success: false,
        error: message,
        correlationId: error.correlationId,
      };
    }

    return {
      success: false,
      error: "An unexpected error occurred. Please try again.",
      correlationId: null,
    };
  }
}