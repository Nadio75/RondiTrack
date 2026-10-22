
const zar = new Intl.NumberFormat('en-ZA', {
  style: 'currency',
  currency: 'ZAR',
})

export function formatCents(cents: number): string {
  if (!Number.isSafeInteger(cents)) {
    throw new RangeError('Amount must be a safe integer number of cents')
  }

  const sign = cents < 0 ? '-' : ''
  const absolute = Math.abs(cents)

  const remainder = absolute % 100
  const rands = (absolute - remainder) / 100

  const decimal =
    `${sign}${rands}.${String(remainder).padStart(2, '0')}`

  return zar.format(decimal as `${number}`)
}


export function parseRandsToCents(value: string): number {
  const trimmed = value.trim()

  if (!/^\d+(?:\.\d{1,2})?$/.test(trimmed)) {
    throw new RangeError(
      'Enter a valid amount with no more than two decimal places',
    )
  }

  const [rands, cents = ''] = trimmed.split('.')

  const amount =
    BigInt(rands) * 100n +
    BigInt(cents.padEnd(2, '0'))

  if (amount > BigInt(Number.MAX_SAFE_INTEGER)) {
    throw new RangeError('Amount is too large')
  }

  return Number(amount)
}


export function centsToInput(cents: number): string {
  if (!Number.isSafeInteger(cents) || cents < 0) {
    throw new RangeError('Amount must be a non-negative safe integer')
  }

  const wholeRands = Math.floor(cents / 100)
  const remainingCents = cents % 100

  return `${wholeRands}.${String(remainingCents).padStart(2, '0')}`
}

