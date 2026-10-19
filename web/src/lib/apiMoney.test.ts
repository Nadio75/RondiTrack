
import { describe, expect, it } from 'vitest'
import { toSafeCents } from './apiMoney'

describe('toSafeCents', () => {
  it('accepts a number of cents', () => {
    expect(toSafeCents(15050)).toBe(15050)
  })

  it('accepts integer cents supplied as a string', () => {
    expect(toSafeCents('15050')).toBe(15050)
  })

  it('preserves ten cents', () => {
    expect(toSafeCents('10')).toBe(10)
  })

  it('rejects missing values', () => {
    expect(() => toSafeCents(undefined)).toThrow(RangeError)
  })

  it('rejects fractional values', () => {
    expect(() => toSafeCents('10.5')).toThrow(RangeError)
  })

  it('rejects unsafe integers', () => {
    expect(() =>
      toSafeCents('9007199254740992'),
    ).toThrow(RangeError)
  })
})
