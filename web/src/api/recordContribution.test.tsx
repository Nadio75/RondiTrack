
// @vitest-environment jsdom

import {
  beforeAll,
  afterAll,
  afterEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'

import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import {
  QueryClient,
  QueryClientProvider,
} from '@tanstack/react-query'
import { act, renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'

import { useRecordContribution } from './queries'
import { getContributionIntent } from '../lib/contributionIntent'

vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true)

// --------------------------------------------------
// TEST API URL
// --------------------------------------------------

const url =
  'http://localhost:8080/api/v1/stokvels/:id/cycles/:cycleId/contributions'

// --------------------------------------------------
// MOCK SERVER
// --------------------------------------------------

const server = setupServer(
  http.post('http://localhost:8080/api/v1/auth/refresh', () => {
    return HttpResponse.json({
      accessToken: 'test-access-token',
      tokenType: 'Bearer',
      expiresIn: 900,
    })
  }),
)

beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' })
})

afterEach(() => {
  server.resetHandlers()
})

afterAll(() => {
  server.close()
})

// --------------------------------------------------
// REACT QUERY WRAPPER
// --------------------------------------------------

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
      mutations: {
        retry: false,
      },
    },
  })

  return (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  )
}

// --------------------------------------------------
// SAMPLE PAYMENT DATA
// --------------------------------------------------

const payment = {
  stokvelId: '11111111-1111-4111-8111-111111111111',
  cycleId: '22222222-2222-4222-8222-222222222222',
  userId: '33333333-3333-4333-8333-333333333333',
  amountInCents: 15050,
  idempotencyKey: '44444444-4444-4444-8444-444444444444',
}

// --------------------------------------------------
// CONTRIBUTION HTTP TESTS
// --------------------------------------------------

describe('record contribution HTTP contract', () => {

  // TEST 1: EXACT CENTS AND IDEMPOTENCY HEADER

  it('sends the exact cents and idempotency header', async () => {
    let receivedKey: string | null = null
    let receivedBody: unknown

    server.use(
      http.post(url, async ({ request }) => {
        receivedKey = request.headers.get('Idempotency-Key')
        receivedBody = await request.json()

        return HttpResponse.json(
          { id: 'contribution-1' },
          { status: 201 },
        )
      }),
    )

    const { result } = renderHook(
      () => useRecordContribution(),
      { wrapper },
    )

    await act(async () => {
      await result.current.mutateAsync(payment)
    })

    expect(receivedKey).toBe(payment.idempotencyKey)

    expect(receivedBody).toEqual({
      userId: payment.userId,
      amountInCents: 15050,
    })
  })

  // TEST 2: 409 CONFLICT

  it('surfaces a 409 conflict', async () => {
    server.use(
      http.post(url, () =>
        HttpResponse.json(
          {
            title: 'Contribution already exists',
          },
          {
            status: 409,
          },
        ),
      ),
    )

    const { result } = renderHook(
      () => useRecordContribution(),
      { wrapper },
    )

    await expect(
      act(async () => {
        await result.current.mutateAsync(payment)
      }),
    ).rejects.toMatchObject({
      status: 409,
    })
  })

  // TEST 3: 422 VALIDATION ERROR

  it('surfaces a 422 validation error', async () => {
    server.use(
      http.post(url, () =>
        HttpResponse.json(
          {
            title: 'Invalid contribution',
          },
          {
            status: 422,
          },
        ),
      ),
    )

    const { result } = renderHook(
      () => useRecordContribution(),
      { wrapper },
    )

    await expect(
      act(async () => {
        await result.current.mutateAsync(payment)
      }),
    ).rejects.toMatchObject({
      status: 422,
    })
  })

  // TEST 4: 429 RATE LIMITING

  it('preserves Retry-After on 429', async () => {
    server.use(
      http.post(url, () =>
        HttpResponse.json(
          {
            title: 'Too many requests',
          },
          {
            status: 429,
            headers: {
              'Retry-After': '30',
            },
          },
        ),
      ),
    )

    const { result } = renderHook(
      () => useRecordContribution(),
      { wrapper },
    )

    await expect(
      act(async () => {
        await result.current.mutateAsync(payment)
      }),
    ).rejects.toMatchObject({
      status: 429,
      retryAfter: 30,
    })
  })

  // TEST 5: RETRY WITH THE SAME IDEMPOTENCY KEY

  it('reuses the same idempotency key after a failed request', async () => {
    const receivedKeys: string[] = []
    let attempts = 0

    server.use(
      http.post(url, ({ request }) => {
        receivedKeys.push(
          request.headers.get('Idempotency-Key') ?? '',
        )

        attempts += 1

        if (attempts === 1) {
          return HttpResponse.error()
        }

        return HttpResponse.json(
          {
            id: 'contribution-1',
          },
          {
            status: 201,
          },
        )
      }),
    )

    const { result } = renderHook(
      () => useRecordContribution(),
      { wrapper },
    )

    const fingerprint = JSON.stringify({
      stokvelId: payment.stokvelId,
      cycleId: payment.cycleId,
      userId: payment.userId,
      amountInCents: payment.amountInCents,
    })

    const firstIntent = getContributionIntent(
      null,
      fingerprint,
    )

    // First request fails.
    await expect(
      act(async () => {
        await result.current.mutateAsync({
          ...payment,
          idempotencyKey: firstIntent.key,
        })
      }),
    ).rejects.toThrow()

    // Retry the unchanged payment.
    const retryIntent = getContributionIntent(
      firstIntent,
      fingerprint,
    )

    // Second request succeeds.
    await act(async () => {
      await result.current.mutateAsync({
        ...payment,
        idempotencyKey: retryIntent.key,
      })
    })

    expect(attempts).toBe(2)

    expect(receivedKeys).toEqual([
      firstIntent.key,
      firstIntent.key,
    ])

    expect(retryIntent.key).toBe(firstIntent.key)
  })

  // TEST 6: 401 TOKEN REFRESH RETAINS IDEMPOTENCY KEY

  it('preserves the idempotency key after a 401 token refresh', async () => {
    const receivedKeys: string[] = []
    let attempts = 0
    let refreshCount = 0

    server.use(
      http.post(
        'http://localhost:8080/api/v1/auth/refresh',
        () => {
          refreshCount += 1

          return HttpResponse.json({
            accessToken: 'refreshed-access-token',
            tokenType: 'Bearer',
            expiresIn: 900,
          })
        },
      ),

      http.post(url, ({ request }) => {
        receivedKeys.push(
          request.headers.get('Idempotency-Key') ?? '',
        )

        attempts += 1

        if (attempts === 1) {
          return HttpResponse.json(
            { title: 'Unauthorized' },
            { status: 401 },
          )
        }

        return HttpResponse.json(
          { id: 'contribution-1' },
          { status: 201 },
        )
      }),
    )

    const { result } = renderHook(
      () => useRecordContribution(),
      { wrapper },
    )

    await act(async () => {
      await result.current.mutateAsync(payment)
    })

    expect(attempts).toBe(2)

    expect(refreshCount).toBeGreaterThanOrEqual(1)

    expect(receivedKeys).toEqual([
      payment.idempotencyKey,
      payment.idempotencyKey,
    ])
  })
})
