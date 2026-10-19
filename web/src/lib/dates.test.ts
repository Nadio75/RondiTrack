
import { describe, expect, it } from 'vitest'
import { formatSastDateTime } from './dates'

describe('formatSastDateTime', () => {
  it('converts UTC into South African time', () => {
    expect(
      formatSastDateTime('2026-10-31T22:30:00Z'),
    ).toBe('01 Nov 2026, 00:30')
  })

  it('handles a missing date', () => {
    expect(formatSastDateTime(null)).toBe('Not scheduled')
  })

  it('rejects invalid dates', () => {
    expect(() =>
      formatSastDateTime('not-a-date'),
    ).toThrow(RangeError)
  })
})
