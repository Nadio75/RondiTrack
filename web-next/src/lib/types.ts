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
