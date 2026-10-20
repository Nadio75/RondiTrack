
import {
  AlertCircle,
  Check,
  CheckCheck,
  Circle,
  Clock3,
  Wallet,
} from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import { formatCents } from '@/lib/money'

type StatusBadgeProps = {
  status: string
  arrearsCents?: number
  className?: string
}

export function StatusBadge({
  status,
  arrearsCents,
  className,
}: StatusBadgeProps) {
  const normalized = status.trim().toLowerCase()

  if (normalized === 'arrears') {
    if (!arrearsCents || arrearsCents <= 0) {
      return null
    }

    return (
      <Badge
        variant="outline"
        className={cn(
          'gap-1.5 border-[#80634f] bg-[#f0e9df] text-[#38281f]',
          'dark:border-[#d4bca5] dark:bg-[#4b382e] dark:text-[#f5e9da]',
          className,
        )}
      >
        <AlertCircle aria-hidden="true" />
        Arrears: {formatCents(arrearsCents)}
      </Badge>
    )
  }

  const statuses = {
    open: {
      label: 'Open',
      Icon: Circle,
      classes:
        'border-[#80634f] bg-[#e8dccb] text-[#38281f] dark:border-[#d4bca5] dark:bg-[#4b382e] dark:text-[#f5e9da]',
    },
    closed: {
      label: 'Closed',
      Icon: Clock3,
      classes:
        'border-[#80634f] bg-[#f0e9df] text-[#38281f] dark:border-[#d4bca5] dark:bg-[#4b382e] dark:text-[#f5e9da]',
    },
    paidout: {
      label: 'Paid out',
      Icon: Wallet,
      classes:
        'border-[#80634f] bg-[#e8dccb] text-[#38281f] dark:border-[#d4bca5] dark:bg-[#4b382e] dark:text-[#f5e9da]',
    },
    pending: {
      label: 'Pending',
      Icon: Clock3,
      classes:
        'border-[#80634f] bg-[#f0e9df] text-[#38281f] dark:border-[#d4bca5] dark:bg-[#4b382e] dark:text-[#f5e9da]',
    },
    paid: {
      label: 'Paid',
      Icon: Check,
      classes:
        'border-[#80634f] bg-[#e8dccb] text-[#38281f] dark:border-[#d4bca5] dark:bg-[#4b382e] dark:text-[#f5e9da]',
    },
    confirmed: {
      label: 'Confirmed',
      Icon: CheckCheck,
      classes:
        'border-[#80634f] bg-[#e8dccb] text-[#38281f] dark:border-[#d4bca5] dark:bg-[#4b382e] dark:text-[#f5e9da]',
    },
  } as const

  const key = normalized.replace(/[\s_-]/g, '')
  const config = statuses[key as keyof typeof statuses]

  if (!config) {
    return (
      <Badge
        variant="outline"
        className={cn(
          'border-border bg-muted text-foreground',
          className,
        )}
      >
        <AlertCircle aria-hidden="true" />
        {status}
      </Badge>
    )
  }

  const Icon = config.Icon

  return (
    <Badge
      variant="outline"
      className={cn('gap-1.5', config.classes, className)}
    >
      <Icon aria-hidden="true" />
      {config.label}
    </Badge>
  )
}
