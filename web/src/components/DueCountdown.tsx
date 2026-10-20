
import { useEffect, useState } from 'react'
import { CalendarClock } from 'lucide-react'

import {
  getDueCountdown,
  msUntilNextSastMidnight,
} from '@/lib/dueCountdown'

type DueCountdownProps = {
  dueAtUtc: string | null | undefined
  status: string
}

export function DueCountdown({
  dueAtUtc,
  status,
}: DueCountdownProps) {
  const [now, setNow] = useState(() => new Date())

  useEffect(() => {
    if (status.trim().toLowerCase() !== 'open' || !dueAtUtc) {
      return
    }

    const delay = msUntilNextSastMidnight(new Date())

    const timeoutId = window.setTimeout(() => {
      setNow(new Date())
    }, delay)

    return () => {
      window.clearTimeout(timeoutId)
    }
  }, [now, dueAtUtc, status])

  const label = getDueCountdown(
    dueAtUtc,
    status,
    now,
  )

  if (!label) {
    return null
  }

  return (
    <p className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary px-3 py-2 text-sm font-medium text-secondary-foreground">
      <CalendarClock
        aria-hidden="true"
        className="size-4 shrink-0"
      />
      {label}
    </p>
  )
}
