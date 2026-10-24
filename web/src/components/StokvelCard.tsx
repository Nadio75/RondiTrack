
import type { HomeStokvel } from '@/api/types'
import { DueCountdown } from '@/components/DueCountdown'
import { RecordContributionForm } from '@/components/RecordContributionForm'

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

import {
  Users,
  CalendarDays,
  Wallet,
  CheckCircle2,
  AlertCircle,
} from 'lucide-react'

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
    <Card className="min-w-0 overflow-hidden rounded-3xl border-border bg-card shadow-sm">
      <CardHeader className="space-y-4 pb-3">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex min-w-0 items-center gap-3">
            <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-rt-teal text-white dark:text-[#142523]">
              <Users className="h-6 w-6" />
            </div>

            <CardTitle className="min-w-0 break-words text-xl font-bold">
              {stokvel.name}
            </CardTitle>
          </div>

          <span className="rounded-full bg-rt-teal px-4 py-2 text-xs font-semibold text-white dark:text-[#142523]">
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

      <CardContent className="space-y-5">
        <div>
          <p className="text-sm text-muted-foreground">
            Contribution amount
          </p>

          <p className="text-2xl font-bold text-rt-teal">
            {contributionAmount}
          </p>
        </div>

        {cycle ? (
          <section
            aria-label="Current contribution cycle"
            className="space-y-4 rounded-2xl border border-border bg-rt-cycle p-5"
          >
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h4 className="font-bold">
                Current cycle: {cycle.period}
              </h4>

              <StatusBadge status={cycle.status} />
            </div>

            <DueCountdown
              dueAtUtc={cycle.dueAtUtc}
              status={cycle.status}
            />

            <div className="grid grid-cols-2 gap-4">
              <div className="min-w-0">
                <p className="flex items-center gap-2 text-sm text-muted-foreground">
                  <CheckCircle2 className="h-4 w-4 text-rt-teal" />
                  Paid
                </p>

                <p className="break-words text-xl font-bold text-rt-teal">
                  {paidAmount}
                </p>
              </div>

              <div className="min-w-0">
                <p className="flex items-center gap-2 text-sm text-muted-foreground">
                  <AlertCircle className="h-4 w-4 text-rt-terracotta" />
                  Still owed
                </p>

                <p className="break-words text-xl font-bold text-rt-terracotta">
                  {owedAmount}
                </p>
              </div>
            </div>

            <div className="space-y-2 border-t border-border/60 pt-3 text-sm">
              <p className="flex items-start gap-2">
                <CalendarDays className="mt-0.5 h-4 w-4 shrink-0 text-rt-teal" />
                <span>
                  <span className="font-semibold">
                    Cycle closes:
                  </span>{' '}
                  {formatSastDateTime(cycle.dueAtUtc)}
                </span>
              </p>

              <p className="flex items-start gap-2">
                <Wallet className="mt-0.5 h-4 w-4 shrink-0 text-rt-teal" />
                <span>
                  <span className="font-semibold">
                    Next payout:
                  </span>{' '}
                  {formatSastDateTime(cycle.nextPayoutDate)}
                </span>
              </p>
            </div>
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

        <RecordContributionForm stokvel={stokvel} />
      </CardContent>
    </Card>
  )
}
