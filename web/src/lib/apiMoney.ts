
export function toSafeCents(
  value: number | string | null | undefined,
): number {
  if (value === null || value === undefined) {
    throw new RangeError('Amount in cents is missing')
  }

  if (typeof value === 'number') {
    if (!Number.isSafeInteger(value)) {
      throw new RangeError('Amount must be a safe integer')
    }

    return value
  }

  if (!/^-?\d+$/.test(value)) {
    throw new RangeError('Amount must contain integer cents')
  }

  const parsed = BigInt(value)

  if (
    parsed > BigInt(Number.MAX_SAFE_INTEGER) ||
    parsed < BigInt(Number.MIN_SAFE_INTEGER)
  ) {
    throw new RangeError('Amount exceeds JavaScript safe integer range')
  }

  return Number(parsed)
}