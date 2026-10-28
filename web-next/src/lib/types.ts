export type HomeCycle = {
  cycleId: string;
  period: string;
  status: string;
  hasPaid: boolean;
  amountOwed: string;
  nextPayoutDate: string | null;
  paidCents: number;
  owedCents: number;
  dueAtUtc: string | null;
};

export type HomeStokvel = {
  stokvelId: string;
  name: string;
  role: string;
  contributionAmount: string;
  contributionAmountCents: number;
  personalArrearsCents: number;
  currentCycle: HomeCycle | null;
};

export type HomeDashboard = {
  stokvels: HomeStokvel[];
};

export type StokvelDetail = {
  id: string;
  name: string;
  contributionAmount: number;
  memberCount: number;
};

export type MemberListItem = {
  userId: string;
  name: string;
  role: string;
  joinedAtUtc: string;
};

export type ContributionListItem = {
  id: string;
  amount: number;
  memberName: string;
  createdAt: string;
  contributionCycleId: string;
};

export type PagedResponse<T> = {
  items: T[];
  nextPageToken: string | null;
};
