import { describe, expect, it, vi } from 'vitest'
import { getContributionIntent } from './contributionIntent'
import { parseRandsToCents } from './money'

describe('contribution idempotency intent', () => {
  it('creates a key for a new contribution', () => {
    const intent = getContributionIntent(
      null,
      'stokvel-1:cycle-1:member-1:15050',
    )

    expect(intent.key).toBeTruthy()
    expect(intent.fingerprint).toBe(
      'stokvel-1:cycle-1:member-1:15050',
    )
  })

  it('reuses the same key when retrying an unchanged contribution', () => {
    const original = getContributionIntent(null, 'payment-A')
    const retry = getContributionIntent(original, 'payment-A')

    expect(retry.key).toBe(original.key)
    expect(retry).toBe(original)
  })

  it('creates a new key when the payment amount changes', () => {
    const original = getContributionIntent(null, 'payment-15050')
    const changed = getContributionIntent(original, 'payment-20000')

    expect(changed.key).not.toBe(original.key)
  })

  it('creates a new key when the member changes', () => {
    const original = getContributionIntent(
      null,
      'cycle-1:member-A:15050',
    )

    const changed = getContributionIntent(
      original,
      'cycle-1:member-B:15050',
    )

    expect(changed.key).not.toBe(original.key)
  })

  it('creates a new key when the cycle changes', () => {
    const original = getContributionIntent(
      null,
      'cycle-1:member-A:15050',
    )

    const changed = getContributionIntent(
      original,
      'cycle-2:member-A:15050',
    )

    expect(changed.key).not.toBe(original.key)
  })

  it('does not create another UUID when retrying', () => {
    const uuid = vi.spyOn(crypto, 'randomUUID')

    try {
      const original = getContributionIntent(null, 'same-payment')
      getContributionIntent(original, 'same-payment')

      expect(uuid).toHaveBeenCalledTimes(1)
    } finally {
      uuid.mockRestore()
    }
  })

  it('reuses a key for equivalent rand amounts', () => {
    const firstAmount = parseRandsToCents('150.5')
    const secondAmount = parseRandsToCents('150.50')

    expect(firstAmount).toBe(15050)
    expect(secondAmount).toBe(15050)

    const firstFingerprint = JSON.stringify({
      stokvelId: 'stokvel-1',
      cycleId: 'cycle-1',
      userId: 'member-1',
      amountInCents: firstAmount,
    })

    const secondFingerprint = JSON.stringify({
      stokvelId: 'stokvel-1',
      cycleId: 'cycle-1',
      userId: 'member-1',
      amountInCents: secondAmount,
    })

    const first = getContributionIntent(null, firstFingerprint)
    const second = getContributionIntent(first, secondFingerprint)

    expect(second.key).toBe(first.key)
  })
})