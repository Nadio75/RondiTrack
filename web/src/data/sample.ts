
import type {
  ContributionItem,
  HomeDashboard,
} from '../api/types'

import type {
  DemoContributionStatus,
} from '../components/ContributionCard'

export const sampleDashboard = {
  stokvels: [
    {
      stokvelId: '11111111-1111-4111-8111-111111111111',
      name: 'Ubuntu Savings Circle',
      role: 'Treasurer',
      contributionAmount: '500.00',
      contributionAmountCents: 50000,
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

export type SampleContribution = {
  contribution: ContributionItem
  demoStatus: DemoContributionStatus
}

export const sampleContributions: SampleContribution[] = [
  {
    contribution: {
      id: '55555555-5555-4555-8555-555555555555',
      amount: 0.10,
      memberName: 'Sample Member A',
      createdAt: '2026-10-31T22:30:00Z',
    },
    demoStatus: 'Pending',
  },
  {
    contribution: {
      id: '66666666-6666-4666-8666-666666666666',
      amount: 500,
      memberName: 'Sample Member B',
      createdAt: '2026-10-19T08:00:00Z',
    },
    demoStatus: 'Paid',
  },
  {
    contribution: {
      id: '77777777-7777-4777-8777-777777777777',
      amount: 750,
      memberName: 'Sample Member C',
      createdAt: '2026-10-18T12:00:00Z',
    },
    demoStatus: 'Confirmed',
  },
]
