
import type { ContributionItem } from '../api/types'
import { formatSastDateTime } from '../lib/dates'
import { formatCents } from '../lib/money'

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
  const amountCents = Math.round(contribution.amount * 100)

  if (!Number.isSafeInteger(amountCents)) {
    throw new RangeError('Contribution amount is out of range')
  }

  return (
    <article className="contribution-card">
      <header className="card-header">
        <h3>{contribution.memberName}</h3>

        {demoStatus && (
          <span className="status-badge">
            {demoStatus} (demo)
          </span>
        )}
      </header>

      <p>
        <strong>Amount:</strong> {formatCents(amountCents)}
      </p>

      <p>
        <strong>Recorded:</strong>{' '}
        {formatSastDateTime(contribution.createdAt)}
      </p>
    </article>
  )
}
