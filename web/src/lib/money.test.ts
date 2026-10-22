
import { describe, expect, it } from 'vitest'
//import { formatCents } from './money'
import {
  formatCents,
  parseRandsToCents,
  centsToInput,
} from './money'

describe('formatCents', () => {
  it('formats ten cents', () => {
    expect(formatCents(10)).toMatch(/R\s*0,10/)
  })

  it('formats zero cents', () => {
    expect(formatCents(0)).toMatch(/R\s*0,00/)
  })

  it('formats 150 rand and 50 cents', () => {
    expect(formatCents(15050)).toMatch(/R\s*150,50/)
  })

  it('formats thousands correctly', () => {
    expect(formatCents(1234567)).toMatch(/R\s*12\s*345,67/)
  })

  it('rejects fractional cents', () => {
    expect(() => formatCents(10.5)).toThrow(RangeError)
  })

  it('rejects unsafe integers', () => {
    expect(() =>
      formatCents(Number.MAX_SAFE_INTEGER + 1),
    ).toThrow(RangeError)
  })
})

describe('parseRandsToCents', () => {
  it('converts R150.50 to 15050 cents', () => {
    expect(parseRandsToCents('150.50')).toBe(15050)
  })

  it('converts whole rands to cents', () => {
    expect(parseRandsToCents('500')).toBe(50000)
  })

  it('converts one cent correctly', () => {
    expect(parseRandsToCents('0.01')).toBe(1)
  })

  it('converts a single decimal digit correctly', () => {
    expect(parseRandsToCents('10.5')).toBe(1050)
  })

  it('rejects more than two decimal places', () => {
    expect(() => parseRandsToCents('10.555')).toThrow(RangeError)
  })

  it('rejects invalid text', () => {
    expect(() => parseRandsToCents('abc')).toThrow(RangeError)
  })

  it('rejects negative amounts', () => {
    expect(() => parseRandsToCents('-10')).toThrow(RangeError)
  })

  it('rejects amounts larger than safe integer cents', () => {
    expect(() =>
      parseRandsToCents('90071992547409.92'),
    ).toThrow(RangeError)
  })

  
it('round-trips every cent from 0 to 100000', () => {
  for (let cents = 0; cents <= 100000; cents++) {
    expect(parseRandsToCents(centsToInput(cents))).toBe(cents)
  }
})
})

