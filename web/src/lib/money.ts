
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
