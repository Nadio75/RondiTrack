import "server-only";

import { apiRequest } from "@/lib/api";
import type { InvitationPreview } from
  "@/app/(app)/join/join-wizard-state";

export async function getInvitationPreview(
  code: string,
): Promise<InvitationPreview> {
  const trimmedCode = code.trim();

  if (!trimmedCode || trimmedCode.length > 128) {
    throw new Error("Enter a valid invitation code.");
  }

  return apiRequest<InvitationPreview>(
    `/api/v1/invites/${encodeURIComponent(trimmedCode)}`,
  );
}
