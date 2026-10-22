// @vitest-environment jsdom

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import type { ComponentProps } from 'react'
import type { HomeStokvel } from '@/api/types'
import { ApiError } from '@/api/ApiError'
import { RecordContributionForm } from './RecordContributionForm'

const mocks = vi.hoisted(() => ({
  mutateAsync: vi.fn(),
  reset: vi.fn(),
  useMe: vi.fn(),
  useMembers: vi.fn(),
  useRecordContribution: vi.fn(),
}))

vi.mock('@/api/queries', () => ({
  useMe: mocks.useMe,
  useMembers: mocks.useMembers,
  useRecordContribution: mocks.useRecordContribution,
}))

const stokvelId = '11111111-1111-4111-8111-111111111111'
const cycleId = '22222222-2222-4222-8222-222222222222'
const userId = '33333333-3333-4333-8333-333333333333'

const stokvel = {
  stokvelId,
  role: 'Member',
  currentCycle: {
    cycleId,
    status: 'Open',
    period: '2026-10',
  },
} as HomeStokvel

type FormProps = ComponentProps<typeof RecordContributionForm>

function renderForm(props: FormProps = { stokvel }) {
  render(<RecordContributionForm {...props} />)

  fireEvent.click(
    screen.getByRole('button', { name: 'Record contribution' }),
  )
}

async function enterAmount(amount: string) {
  fireEvent.change(screen.getByLabelText('Amount (R)'), {
    target: { value: amount },
  })
}

async function submit() {
  await act(async () => {
    fireEvent.click(
      screen.getByRole('button', { name: 'Submit contribution' }),
    )
  })
}

beforeEach(() => {
  vi.clearAllMocks()

  mocks.useMe.mockReturnValue({
    data: { id: userId },
  })

  mocks.useMembers.mockReturnValue({
    data: { pages: [{ items: [] }] },
    hasNextPage: false,
    isFetchingNextPage: false,
    fetchNextPage: vi.fn(),
  })

  mocks.useRecordContribution.mockReturnValue({
    mutateAsync: mocks.mutateAsync,
    reset: mocks.reset,
    isPending: false,
    isError: false,
    error: null,
  })
})

afterEach(() => {
  cleanup()
})

describe('RecordContributionForm', () => {
  it('submits exact cents with an idempotency key', async () => {
    mocks.mutateAsync.mockResolvedValue({ id: 'contribution-1' })

    renderForm()
    await enterAmount('150.50')
    await submit()

    await waitFor(() => {
      expect(mocks.mutateAsync).toHaveBeenCalledTimes(1)
    })

    expect(mocks.mutateAsync).toHaveBeenCalledWith({
      stokvelId,
      cycleId,
      userId,
      amountInCents: 15050,
      idempotencyKey: expect.any(String),
    })
  })

  it('reuses the same key when retrying an unchanged payment', async () => {
    mocks.mutateAsync
      .mockRejectedValueOnce(new Error('Network failure'))
      .mockResolvedValueOnce({ id: 'contribution-1' })

    renderForm()
    await enterAmount('150.50')

    await submit()

    await waitFor(() => {
      expect(mocks.mutateAsync).toHaveBeenCalledTimes(1)
    })

    await submit()

    await waitFor(() => {
      expect(mocks.mutateAsync).toHaveBeenCalledTimes(2)
    })

    const first = mocks.mutateAsync.mock.calls[0][0]
    const second = mocks.mutateAsync.mock.calls[1][0]

    expect(first.idempotencyKey).toBe(second.idempotencyKey)
    expect(first.amountInCents).toBe(15050)
    expect(second.amountInCents).toBe(15050)
  })

  it('generates a new key when the amount changes and changes back', async () => {
    mocks.mutateAsync.mockRejectedValue(new Error('Network failure'))

    renderForm()
    await enterAmount('150.50')
    await submit()

    await waitFor(() => {
      expect(mocks.mutateAsync).toHaveBeenCalledTimes(1)
    })

    await enterAmount('200.00')
    await enterAmount('150.50')
    await submit()

    await waitFor(() => {
      expect(mocks.mutateAsync).toHaveBeenCalledTimes(2)
    })

    const first = mocks.mutateAsync.mock.calls[0][0]
    const second = mocks.mutateAsync.mock.calls[1][0]

    expect(first.idempotencyKey).not.toBe(second.idempotencyKey)
  })

  it('shows the conflict message for a 409 response', async () => {
    const conflict = new ApiError(
      409,
      'Contribution already exists',
    )

    mocks.mutateAsync.mockRejectedValue(conflict)

    mocks.useRecordContribution.mockReturnValue({
      mutateAsync: mocks.mutateAsync,
      reset: mocks.reset,
      isPending: false,
      isError: true,
      error: conflict,
    })

    renderForm()
    await enterAmount('150.50')
    await submit()

    expect(
      screen.getByText(/may already have been recorded/i),
    ).toBeTruthy()
  })

  it('disables submission during a 429 waiting period', async () => {
    const rateLimit = new ApiError(
      429,
      'Too many requests',
      30,
    )

    mocks.mutateAsync.mockRejectedValue(rateLimit)

    renderForm()
    await enterAmount('150.50')
    await submit()

    await waitFor(() => {
      expect(
        screen.getByRole('button', { name: /wait 30s/i }),
      ).toHaveProperty('disabled', true)
    })

    expect(mocks.mutateAsync).toHaveBeenCalledTimes(1)
  })
})