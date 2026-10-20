
import { describe, expect, it } from 'vitest'

import type {
  ContributionCycle,
  ContributionItem,
} from '@/api/types'

import {
  groupContributionsByCycle,
  UNKNOWN_CYCLE_ID,
} from './groupContributions'

const cycles: ContributionCycle[] = [
  {
    id: 'older',
    stokvelId: 'stokvel-1',
    period: '2026-09',
    targetAmount: 500,
    createdAt: '2026-09-01T08:00:00Z',
    status: 'Closed',
  },
  {
    id: 'newer',
    stokvelId: 'stokvel-1',
    period: '2026-10',
    targetAmount: 500,
    createdAt: '2026-10-01T08:00:00Z',
    status: 'Open',
  },
]

const contributions: ContributionItem[] = [
  {
    id: 'old-payment',
    contributionCycleId: 'newer',
    amount: 100,
    memberName: 'A',
    createdAt: '2026-10-02T08:00:00Z',
  },
  {
    id: 'new-payment',
    contributionCycleId: 'newer',
    amount: 200,
    memberName: 'B',
    createdAt: '2026-10-03T08:00:00Z',
  },
  {
    id: 'unknown-payment',
    contributionCycleId: 'missing',
    amount: 50,
    memberName: 'C',
    createdAt: '2026-10-04T08:00:00Z',
  },
]

describe('groupContributionsByCycle', () => {
  it('orders cycles newest first', () => {
    const result = groupContributionsByCycle(
      cycles,
      contributions,
    )

    expect(result.map((group) => group.id)).toEqual([
      'newer',
      'older',
      UNKNOWN_CYCLE_ID,
    ])
  })

  it('orders contributions newest first within a cycle', () => {
    const result = groupContributionsByCycle(
      cycles,
      contributions,
    )

    expect(
      result[0].contributions.map((item) => item.id),
    ).toEqual(['new-payment', 'old-payment'])
  })

  it('includes cycles with no contributions', () => {
    const result = groupContributionsByCycle(cycles, [])

    expect(result).toHaveLength(2)
    expect(result[0].contributions).toEqual([])
    expect(result[1].contributions).toEqual([])
  })

  it('preserves unmatched contributions in the last group', () => {
    const result = groupContributionsByCycle(
      cycles,
      contributions,
    )

    const unknown = result.at(-1)

    expect(unknown?.id).toBe(UNKNOWN_CYCLE_ID)
    expect(
      unknown?.contributions.map((item) => item.id),
    ).toEqual(['unknown-payment'])
  })

  it('does not modify the original arrays', () => {
    const originalCycleIds = cycles.map((cycle) => cycle.id)
    const originalContributionIds = contributions.map(
      (item) => item.id,
    )

    groupContributionsByCycle(cycles, contributions)

    expect(cycles.map((cycle) => cycle.id)).toEqual(
      originalCycleIds,
    )
    expect(
      contributions.map((item) => item.id),
    ).toEqual(originalContributionIds)
  })
})
