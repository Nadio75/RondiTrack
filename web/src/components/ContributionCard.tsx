
import type { ContributionItem } from '@/api/types'

import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'

import { StatusBadge } from '@/components/StatusBadge'
import { formatSastDateTime } from '@/lib/dates'
import { formatCents } from '@/lib/money'

export type DemoContributionStatus =
  | 'Pending'
  | 'Paid'
  | 'Confirmed'

type ContributionCardProps = {
  contribution: ContributionItem
  demoStatus?: DemoContributionStatus
}

export function ContributionCard({
  contribution,
  demoStatus,
}: ContributionCardProps) {
  const amountCents = Math.round(
    contribution.amount * 100,
  )

  if (!Number.isSafeInteger(amountCents)) {
    throw new RangeError(
      'Contribution amount is out of range',
    )
  }

  return (
    <Card className="min-w-0" size="sm">
      <CardHeader>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <CardTitle className="min-w-0 break-words">
            {contribution.memberName}
          </CardTitle>

          {demoStatus && (
            <div className="flex items-center gap-2">
              <StatusBadge status={demoStatus} />
              <span className="text-xs text-muted-foreground">
                Demo
              </span>
            </div>
          )}
        </div>
      </CardHeader>

      <CardContent className="space-y-2">
        <p>
          <span className="font-medium">Amount:</span>{' '}
          {formatCents(amountCents)}
        </p>

        <p className="text-sm text-muted-foreground">
          Recorded:{' '}
          {formatSastDateTime(contribution.createdAt)}
        </p>
      </CardContent>
    </Card>
  )
}