
const SAST_OFFSET_MS = 2 * 60 * 60 * 1000
const DAY_MS = 24 * 60 * 60 * 1000

function getSastDayNumber(date: Date): number {
  return Math.floor(
    (date.getTime() + SAST_OFFSET_MS) / DAY_MS,
  )
}

function parseDeadline(
  dueAtUtc: string,
): Date {
  // Date-only values represent the END of that SAST day.
  // Example: 2026-10-31 => 2026-10-31T22:00:00Z.
  if (/^\d{4}-\d{2}-\d{2}$/.test(dueAtUtc)) {
    const startOfNextDayUtc =
      Date.parse(`${dueAtUtc}T00:00:00Z`) +
      DAY_MS -
      SAST_OFFSET_MS

    const deadline = new Date(startOfNextDayUtc)

    if (Number.isNaN(deadline.getTime())) {
      throw new RangeError('Invalid due date')
    }

    return deadline
  }

  const deadline = new Date(dueAtUtc)

  if (Number.isNaN(deadline.getTime())) {
    throw new RangeError('Invalid due date')
  }

  return deadline
}

export function getDueCountdown(
  dueAtUtc: string | null | undefined,
  status: string,
  now: Date,
): string | null {
  if (status.trim().toLowerCase() !== 'open') {
    return null
  }

  if (!dueAtUtc) {
    return null
  }

  const deadline = parseDeadline(dueAtUtc)

  // The midnight instant belongs to the previous
  // calendar day's deadline.
  const deadlineDay = getSastDayNumber(
    new Date(deadline.getTime() - 1),
  )

  const today = getSastDayNumber(now)
  const daysRemaining = deadlineDay - today

  if (daysRemaining > 1) {
    return `Due in ${daysRemaining} days`
  }

  if (daysRemaining === 1) {
    return 'Due tomorrow'
  }

  if (daysRemaining === 0) {
    return 'Due today'
  }

  return `Overdue by ${Math.abs(daysRemaining)} ${
    Math.abs(daysRemaining) === 1 ? 'day' : 'days'
  }`
}

export function msUntilNextSastMidnight(
  now: Date,
): number {
  const currentSastMs =
    now.getTime() + SAST_OFFSET_MS

  const nextSastMidnightMs =
    (Math.floor(currentSastMs / DAY_MS) + 1) *
    DAY_MS

  return nextSastMidnightMs - currentSastMs
}
