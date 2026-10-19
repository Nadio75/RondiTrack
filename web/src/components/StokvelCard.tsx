
import type { HomeStokvel } from '../api/types'
import { toSafeCents } from '../lib/apiMoney'
import { formatCents } from '../lib/money'
import { formatSastDateTime } from '../lib/dates'

type StokvelCardProps = {
  stokvel: HomeStokvel
}

export function StokvelCard({ stokvel }: StokvelCardProps) {
  const cycle = stokvel.currentCycle

  const contributionAmount = formatCents(
    toSafeCents(stokvel.contributionAmountCents),
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
    <article className="stokvel-card">
      <header className="card-header">
        <h2>{stokvel.name}</h2>
        <span className="role-badge">{stokvel.role}</span>
      </header>

      <p>
        <strong>Contribution amount:</strong>{' '}
        {contributionAmount}
      </p>

      {cycle ? (
        <section aria-label="Current contribution cycle">
          <h3>Current cycle: {cycle.period}</h3>

          <p>
            <strong>Cycle status:</strong> {cycle.status}
          </p>

          <p>
            <strong>Paid:</strong> {paidAmount}
          </p>

          <p>
            <strong>Still owed:</strong> {owedAmount}
          </p>

          <p>
            <strong>Cycle closes:</strong>{' '}
            {formatSastDateTime(cycle.dueAtUtc)}
          </p>

          <p>
            <strong>Next payout:</strong>{' '}
            {formatSastDateTime(cycle.nextPayoutDate)}
          </p>
        </section>
      ) : (
        <p>No active contribution cycle.</p>
      )}

      {isTreasurer && (
        <div className="treasurer-actions">
          <button type="button" disabled>
            Manage contributions (coming soon)
          </button>
        </div>
      )}
    </article>
  )
}
