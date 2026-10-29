import "server-only";

import { apiRequest } from "@/lib/api";
import type { MemberListItem, PagedResponse } from "@/lib/types";

const PAGE_SIZE = 100;

export async function getContributionHistoryMembers(
  stokvelId: string,
): Promise<MemberListItem[]> {
  const members: MemberListItem[] = [];
  const seenTokens = new Set<string>();

  let pageToken: string | null = null;

  do {
    const params = new URLSearchParams({
      page_size: String(PAGE_SIZE),
    });

    if (pageToken) {
      params.set("page_token", pageToken);
    }

    const page = await apiRequest<PagedResponse<MemberListItem>>(
      `/api/v1/stokvels/${encodeURIComponent(stokvelId)}` +
        `/members?${params.toString()}`,
    );

    members.push(...page.items);

    const nextToken = page.nextPageToken;

    if (!nextToken) {
      break;
    }

    if (seenTokens.has(nextToken)) {
      throw new Error("Member pagination returned a repeated bookmark.");
    }

    seenTokens.add(nextToken);
    pageToken = nextToken;
  } while (pageToken);

  return members;
}

export function isAllowedHistoryMember(
  memberId: string,
  members: MemberListItem[],
): boolean {
  if (!memberId) {
    return true;
  }

  return members.some(
    (member) =>
      member.userId.toLowerCase() === memberId.toLowerCase(),
  );
}
