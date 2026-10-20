
import type {
  ContributionCycle,
  ContributionItem,
} from '@/api/types'

export type ContributionGroup = {
  id: string
  cycle: ContributionCycle | null
  contributions: ContributionItem[]
}

export const UNKNOWN_CYCLE_ID = '__unknown_cycle__'

function newestFirst(
  first: { createdAt: string },
  second: { createdAt: string },
): number {
  return (
    Date.parse(second.createdAt) -
    Date.parse(first.createdAt)
  )
}

export function groupContributionsByCycle(
  cycles: readonly ContributionCycle[],
  contributions: readonly ContributionItem[],
): ContributionGroup[] {
  const sortedCycles = [...cycles].sort(newestFirst)
  const sortedContributions = [...contributions].sort(newestFirst)

  const groups: ContributionGroup[] = sortedCycles.map(
    (cycle) => ({
      id: cycle.id,
      cycle,
      contributions: [],
    }),
  )

  const groupsById = new Map(
    groups.map((group) => [group.id, group]),
  )

  const unknownContributions: ContributionItem[] = []

  for (const contribution of sortedContributions) {
    const group = contribution.contributionCycleId
      ? groupsById.get(contribution.contributionCycleId)
      : undefined

    if (group) {
      group.contributions.push(contribution)
    } else {
      unknownContributions.push(contribution)
    }
  }

  if (unknownContributions.length > 0) {
    groups.push({
      id: UNKNOWN_CYCLE_ID,
      cycle: null,
      contributions: unknownContributions,
    })
  }

  return groups
}
