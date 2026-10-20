
import type { HomeStokvel } from '@/api/types'
import { DueCountdown } from '@/components/DueCountdown'
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'

import { Button } from '@/components/ui/button'
import { StatusBadge } from '@/components/StatusBadge'

import { toSafeCents } from '@/lib/apiMoney'
import { formatCents } from '@/lib/money'
import { formatSastDateTime } from '@/lib/dates'

type StokvelCardProps = {
  stokvel: HomeStokvel
}

export function StokvelCard({ stokvel }: StokvelCardProps) {
  const cycle = stokvel.currentCycle

  const contributionAmount = formatCents(
    toSafeCents(stokvel.contributionAmountCents),
  )

  const personalArrears = toSafeCents(
    stokvel.personalArrearsCents,
  )

  const paidAmount = cycle
    ? formatCents(toSafeCents(cycle.paidCents))
    : null

  const owedAmount = cycle
    ? formatCents(toSafeCents(cycle.owedCents))
    : null

  const isTreasurer =
    stokvel.role.toLowerCase() === 'treasurer'

  return (
    <Card className="min-w-0">
      <CardHeader>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <CardTitle className="min-w-0 break-words text-lg">
            {stokvel.name}
          </CardTitle>

          <span className="rounded-full bg-secondary px-3 py-1 text-xs font-medium text-secondary-foreground">
            {isTreasurer
              ? "You're the Treasurer"
              : stokvel.role}
          </span>
        </div>

        <StatusBadge
          status="Arrears"
          arrearsCents={personalArrears}
          className="mt-2"
        />
      </CardHeader>

      <CardContent className="space-y-4">
        <div>
          <p className="text-sm text-muted-foreground">
            Contribution amount
          </p>
          <p className="text-xl font-semibold">
            {contributionAmount}
          </p>
        </div>

        {cycle ? (
          <section
            aria-label="Current contribution cycle"
            className="space-y-3 rounded-xl border border-border bg-muted/40 p-4"
          >
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h4 className="font-semibold">
                Current cycle: {cycle.period}
              </h4>
              <StatusBadge status={cycle.status} />
            </div>
             <DueCountdown
                dueAtUtc={cycle.dueAtUtc}
                status={cycle.status}
              />

            <div className="grid gap-3 sm:grid-cols-2">
              <div>
                <p className="text-sm text-muted-foreground">
                  Paid
                </p>
                <p className="font-semibold">
                  {paidAmount}
                </p>
              </div>

              <div>
                <p className="text-sm text-muted-foreground">
                  Still owed
                </p>
                <p className="font-semibold">
                  {owedAmount}
                </p>
              </div>
            </div>

            <p className="text-sm">
              <span className="font-medium">
                Cycle closes:
              </span>{' '}
              {formatSastDateTime(cycle.dueAtUtc)}
            </p>

            <p className="text-sm">
              <span className="font-medium">
                Next payout:
              </span>{' '}
              {formatSastDateTime(cycle.nextPayoutDate)}
            </p>
          </section>
        ) : (
          <p className="text-sm text-muted-foreground">
            No active contribution cycle.
          </p>
        )}

        {isTreasurer && (
          <Button
            type="button"
            variant="outline"
            disabled
            className="w-full"
          >
            Manage contributions (coming soon)
          </Button>
        )}
      </CardContent>
    </Card>
  )
}