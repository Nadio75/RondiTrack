
import type {
  ContributionCycle,
  ContributionItem,
  HomeDashboard,
} from '@/api/types'

import type {
  DemoContributionStatus,
} from '@/components/ContributionCard'

export const sampleDashboard = {
  stokvels: [
    {
      stokvelId: '11111111-1111-4111-8111-111111111111',
      name: 'Ubuntu Savings Circle',
      role: 'Treasurer',
      contributionAmount: '500.00',
      contributionAmountCents: 50000,
      personalArrearsCents: 0,
      currentCycle: {
        cycleId: '22222222-2222-4222-8222-222222222222',
        period: '2026-10',
        status: 'Open',
        hasPaid: true,
        amountOwed: '0.00',
        paidCents: 50000,
        owedCents: 0,
        dueAtUtc: '2026-10-31T22:00:00Z',
        nextPayoutDate: null,
      },
    },
    {
      stokvelId: '33333333-3333-4333-8333-333333333333',
      name: 'Future Builders Stokvel',
      role: 'Member',
      contributionAmount: '750.00',
      contributionAmountCents: 75000,
      personalArrearsCents: 15000,
      currentCycle: {
        cycleId: '44444444-4444-4444-8444-444444444444',
        period: '2026-10',
        status: 'Open',
        hasPaid: false,
        amountOwed: '750.00',
        paidCents: 0,
        owedCents: 75000,
        dueAtUtc: '2026-10-31T22:30:00Z',
        nextPayoutDate: null,
      },
    },
  ],
} satisfies HomeDashboard

const STOKVEL_ID = '11111111-1111-4111-8111-111111111111'

const OPEN_CYCLE_ID = '22222222-2222-4222-8222-222222222222'
const CLOSED_CYCLE_ID = '88888888-8888-4888-8888-888888888888'
const PAID_OUT_CYCLE_ID = '99999999-9999-4999-8999-999999999999'

export const sampleCycles: ContributionCycle[] = [
  {
    id: OPEN_CYCLE_ID,
    stokvelId: STOKVEL_ID,
    period: '2026-10',
    targetAmount: 1500,
    createdAt: '2026-10-01T08:00:00Z',
    status: 'Open',
    isClosed: false,
    dueAtUtc: '2026-10-31T22:00:00Z',
    totalCollectedCents: 125000,
    amountDueCents: 25000,
  },
  {
    id: CLOSED_CYCLE_ID,
    stokvelId: STOKVEL_ID,
    period: '2026-09',
    targetAmount: 1500,
    createdAt: '2026-09-01T08:00:00Z',
    status: 'Closed',
    isClosed: true,
    closedAtUtc: '2026-09-30T22:00:00Z',
    dueAtUtc: '2026-09-30T22:00:00Z',
    totalCollectedCents: 150000,
    amountDueCents: 0,
  },
  {
    id: PAID_OUT_CYCLE_ID,
    stokvelId: STOKVEL_ID,
    period: '2026-08',
    targetAmount: 1500,
    createdAt: '2026-08-01T08:00:00Z',
    status: 'PaidOut',
    isClosed: true,
    closedAtUtc: '2026-08-31T22:00:00Z',
    dueAtUtc: '2026-08-31T22:00:00Z',
    totalCollectedCents: 150000,
    amountDueCents: 0,
  },
]

export type SampleContribution = {
  contribution: ContributionItem
  demoStatus: DemoContributionStatus
}

export const sampleContributions: SampleContribution[] = [
  {
    contribution: {
      id: '55555555-5555-4555-8555-555555555555',
      contributionCycleId: OPEN_CYCLE_ID,
      amount: 500,
      memberName: 'Sample Member A',
      createdAt: '2026-10-19T08:00:00Z',
    },
    demoStatus: 'Pending',
  },
  {
    contribution: {
      id: '66666666-6666-4666-8666-666666666666',
      contributionCycleId: OPEN_CYCLE_ID,
      amount: 750,
      memberName: 'Sample Member B',
      createdAt: '2026-10-20T08:00:00Z',
    },
    demoStatus: 'Paid',
  },
  {
    contribution: {
      id: '77777777-7777-4777-8777-777777777777',
      contributionCycleId: CLOSED_CYCLE_ID,
      amount: 1500,
      memberName: 'Sample Member C',
      createdAt: '2026-09-18T12:00:00Z',
    },
    demoStatus: 'Confirmed',
  },
  {
    contribution: {
      id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      contributionCycleId: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
      amount: 0.1,
      memberName: 'Unmatched Sample Member',
      createdAt: '2026-10-21T08:00:00Z',
    },
    demoStatus: 'Pending',
  },
]
