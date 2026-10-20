
import { useState } from 'react'
import { ChevronDown, ChevronUp } from 'lucide-react'
import { DueCountdown } from '@/components/DueCountdown'
import type {
    ContributionCycle,
    ContributionItem,
} from '@/api/types'

import { Button } from '@/components/ui/button'
import {
    Card,
    CardContent,
    CardHeader,
} from '@/components/ui/card'
import { ContributionCard } from '@/components/ContributionCard'
import type {
    DemoContributionStatus,
} from '@/components/ContributionCard'
import { StatusBadge } from '@/components/StatusBadge'

import {
    groupContributionsByCycle,
} from '@/lib/groupContributions'

import { formatCents } from '@/lib/money'
import { toSafeCents } from '@/lib/apiMoney'
import { formatSastDateTime } from '@/lib/dates'

type ContributionListProps = {
    cycles: readonly ContributionCycle[]
    contributions: readonly ContributionItem[]
    demoStatuses?: Readonly<Record<string, DemoContributionStatus>>
}

export function ContributionList({
    cycles,
    contributions,
    demoStatuses = {},
}: ContributionListProps) {
    const groups = groupContributionsByCycle(
        cycles,
        contributions,
    )

    // Store only user overrides, not the grouped data.
    const [expandedOverrides, setExpandedOverrides] =
        useState<Record<string, boolean>>({})

    function isExpanded(id: string, status?: string): boolean {
        const override = expandedOverrides[id]

        if (override !== undefined) {
            return override
        }

        return status?.trim().toLowerCase() === 'open'
    }

    const allExpanded =
        groups.length > 0 &&
        groups.every((group) =>
            isExpanded(group.id, group.cycle?.status),
        )

    function toggleAll() {
        const nextExpanded = !allExpanded

        setExpandedOverrides(
            Object.fromEntries(
                groups.map((group) => [group.id, nextExpanded]),
            ),
        )
    }

    return (
        <div className="space-y-4">
            <div className="flex justify-end">
                <Button
                    type="button"
                    variant="outline"
                    onClick={toggleAll}
                    disabled={groups.length === 0}
                >
                    {allExpanded ? 'Collapse all' : 'Expand all'}
                </Button>
            </div>

            {groups.length === 0 && (
                <p className="text-muted-foreground">
                    No contribution cycles yet.
                </p>
            )}

            {groups.map((group) => {
                const cycle = group.cycle
                const expanded = isExpanded(
                    group.id,
                    cycle?.status,
                )

                const headingId = `cycle-heading-${group.id}`
                const contentId = `cycle-content-${group.id}`

                return (
                    <Card
                        key={group.id}
                        className="min-w-0"
                        aria-labelledby={headingId}
                    >
                        <CardHeader>
                            <div className="flex flex-wrap items-start justify-between gap-3">
                                <div className="min-w-0 space-y-2">
                                    <h3
                                        id={headingId}
                                        className="break-words text-lg font-semibold"
                                    >
                                        {cycle
                                            ? `Cycle: ${cycle.period}`
                                            : 'Unknown cycle'}
                                    </h3>

                                    {cycle && (
                                        <div className="flex flex-wrap items-center gap-2">
                                            <StatusBadge status={cycle.status ?? 'Unknown'} />

                                            <DueCountdown
                                                dueAtUtc={cycle.dueAtUtc}
                                                status={cycle.status ?? 'Unknown'}
                                            />
                                        </div>
                                    )}
                                </div>

                                <Button
                                    type="button"
                                    variant="outline"
                                    aria-expanded={expanded}
                                    aria-controls={contentId}
                                    onClick={() =>
                                        setExpandedOverrides((previous) => ({
                                            ...previous,
                                            [group.id]: !expanded,
                                        }))
                                    }
                                >
                                    {expanded ? (
                                        <ChevronUp aria-hidden="true" />
                                    ) : (
                                        <ChevronDown aria-hidden="true" />
                                    )}
                                    {expanded ? 'Collapse' : 'Expand'}
                                </Button>
                            </div>

                            {cycle && (
                                <div className="grid gap-2 pt-2 text-sm sm:grid-cols-3">
                                    <p>
                                        <span className="text-muted-foreground">
                                            Due:
                                        </span>{' '}
                                        {formatSastDateTime(cycle.dueAtUtc)}
                                    </p>

                                    <p>
                                        <span className="text-muted-foreground">
                                            Collected:
                                        </span>{' '}
                                        {cycle.totalCollectedCents == null
                                            ? 'Not available'
                                            : formatCents(
                                                toSafeCents(
                                                    cycle.totalCollectedCents,
                                                ),
                                            )}
                                    </p>

                                    <p>
                                        <span className="text-muted-foreground">
                                            Amount due:
                                        </span>{' '}
                                        {cycle.amountDueCents == null
                                            ? 'Not available'
                                            : formatCents(
                                                toSafeCents(
                                                    cycle.amountDueCents,
                                                ),
                                            )}
                                    </p>
                                </div>
                            )}
                        </CardHeader>

                        <CardContent>
                            <div id={contentId} hidden={!expanded}>
                                {group.contributions.length === 0 ? (
                                    <p className="text-sm text-muted-foreground">
                                        No contributions yet.
                                    </p>
                                ) : (
                                    <div className="grid gap-3 md:grid-cols-2">
                                        {group.contributions.map(
                                            (contribution) => (
                                                <ContributionCard
                                                    key={contribution.id}
                                                    contribution={contribution}
                                                    demoStatus={
                                                        demoStatuses[contribution.id]
                                                    }
                                                />
                                            ),
                                        )}
                                    </div>
                                )}
                            </div>
                        </CardContent>
                    </Card>
                )
            })}
        </div>
    )
}