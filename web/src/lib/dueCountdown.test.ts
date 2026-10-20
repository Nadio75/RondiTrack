
import { describe, expect, it } from 'vitest'

import {
  getDueCountdown,
  msUntilNextSastMidnight,
} from './dueCountdown'

describe('getDueCountdown', () => {
  it('handles a future deadline', () => {
    expect(
      getDueCountdown(
        '2026-10-31T22:00:00Z',
        'Open',
        new Date('2026-10-20T12:00:00Z'),
      ),
    ).toBe('Due in 11 days')
  })

  it('handles tomorrow', () => {
    expect(
      getDueCountdown(
        '2026-10-31T22:00:00Z',
        'Open',
        new Date('2026-10-30T12:00:00Z'),
      ),
    ).toBe('Due tomorrow')
  })

  it('handles the deadline day', () => {
    expect(
      getDueCountdown(
        '2026-10-31T22:00:00Z',
        'Open',
        new Date('2026-10-31T21:30:00Z'),
      ),
    ).toBe('Due today')
  })

  it('handles overdue deadlines', () => {
    expect(
      getDueCountdown(
        '2026-10-31T22:00:00Z',
        'Open',
        new Date('2026-10-31T22:00:01Z'),
      ),
    ).toBe('Overdue by 1 day')
  })

  it('supports date-only deadlines', () => {
    expect(
      getDueCountdown(
        '2026-10-31',
        'Open',
        new Date('2026-10-31T12:00:00Z'),
      ),
    ).toBe('Due today')
  })

  it('hides the countdown for closed cycles', () => {
    expect(
      getDueCountdown(
        '2026-10-31T22:00:00Z',
        'Closed',
        new Date('2026-10-20T12:00:00Z'),
      ),
    ).toBeNull()
  })

  it('hides the countdown for paid-out cycles', () => {
    expect(
      getDueCountdown(
        '2026-10-31T22:00:00Z',
        'PaidOut',
        new Date('2026-10-20T12:00:00Z'),
      ),
    ).toBeNull()
  })

  it('handles a missing deadline', () => {
    expect(
      getDueCountdown(
        null,
        'Open',
        new Date('2026-10-20T12:00:00Z'),
      ),
    ).toBeNull()
  })
})

describe('msUntilNextSastMidnight', () => {
  it('calculates the time until SAST midnight', () => {
    expect(
      msUntilNextSastMidnight(
        new Date('2026-10-31T21:30:00Z'),
      ),
    ).toBe(30 * 60 * 1000)
  })

  it('returns a full day immediately after midnight', () => {
    expect(
      msUntilNextSastMidnight(
        new Date('2026-10-31T22:00:00Z'),
      ),
    ).toBe(24 * 60 * 60 * 1000)
  })
})
