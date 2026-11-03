"use server";

import { ApiError, apiRequest } from "@/lib/api";
import { getInvitationPreview } from "@/lib/invitations";
import type { InvitationPreview } from "./join-wizard-state";

export type PreviewResult =
  | {
      success: true;
      invitation: InvitationPreview;
    }
  | {
      success: false;
      error: string;
      correlationId: string | null;
    };

export async function previewInvitationAction(
  code: string,
): Promise<PreviewResult> {
  try {
    const invitation = await getInvitationPreview(code);

    return {
      success: true,
      invitation,
    };
  } catch (error) {
    if (error instanceof ApiError) {
      if (error.status === 404) {
        return {
          success: false,
          error: "This invitation is invalid or has expired.",
          correlationId: error.correlationId,
        };
      }

      if (error.status === 429) {
        return {
          success: false,
          error: "Too many attempts. Please wait before trying again.",
          correlationId: error.correlationId,
        };
      }

      return {
        success: false,
        error: error.message,
        correlationId: error.correlationId,
      };
    }

    return {
      success: false,
      error: "Unable to check this invitation. Please try again.",
      correlationId: null,
    };
  }
}

export type JoinResult =
  | {
      success: true;
      stokvelId: string;
    }
  | {
      success: false;
      error: string;
      correlationId: string | null;
    };

export async function joinStokvelAction(
  code: string,
): Promise<JoinResult> {
  const invitationCode = code.trim();

  if (!invitationCode || invitationCode.length > 128) {
    return {
      success: false,
      error: "Please enter a valid invitation code.",
      correlationId: null,
    };
  }

  try {
    const result = await apiRequest<{
      stokvelId: string;
      message: string;
    }>(
      `/api/v1/invites/${encodeURIComponent(invitationCode)}/join`,
      { method: "POST" },
    );

    return {
      success: true,
      stokvelId: result.stokvelId,
    };
  } catch (error) {
    if (error instanceof ApiError) {
      let message: string;

      switch (error.status) {
        case 404:
          message = "This invitation is invalid or has expired.";
          break;
        case 409:
          message = "You already belong to this stokvel.";
          break;
        case 403:
          message = "You are not authorized to join this stokvel.";
          break;
        case 429:
          message = "Too many attempts. Please wait before trying again.";
          break;
        default:
          message = "Unable to join the stokvel. Please try again.";
      }

      return {
        success: false,
        error: message,
        correlationId: error.correlationId,
      };
    }

    return {
      success: false,
      error: "A network or server error occurred. Please try again.",
      correlationId: null,
    };
  }
}