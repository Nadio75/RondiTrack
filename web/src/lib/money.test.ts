
import { describe, expect, it } from 'vitest'
import { formatCents } from './money'

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
