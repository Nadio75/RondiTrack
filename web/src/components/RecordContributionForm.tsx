import { useEffect, useRef, useState } from 'react'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import {
  useMe,
  useMembers,
  useRecordContribution,
} from '@/api/queries'
import { ApiError } from '@/api/ApiError'
import { parseRandsToCents } from '@/lib/money'
import type { HomeStokvel } from '@/api/types'
import {
  getContributionIntent,
  type ContributionIntent,
} from '@/lib/contributionIntent'

const MAX_CENTS = 2_147_483_647

const schema = z.object({
  userId: z.uuid('Select a valid member'),
  amount: z.string().min(1, 'Enter an amount').refine(
    (value) => {
      try {
        const cents = parseRandsToCents(value)
        return cents > 0 && cents <= MAX_CENTS
      } catch {
        return false
      }
    },
    'Enter a positive amount with at most two decimals',
  ),
})

type FormValues = z.infer<typeof schema>

type Props = {
  stokvel: HomeStokvel
}

export function RecordContributionForm({ stokvel }: Props) {
  const me = useMe()
  const members = useMembers(stokvel.stokvelId)
  const mutation = useRecordContribution()

  const [success, setSuccess] = useState(false)
  const [showForm, setShowForm] = useState(false)
  const [retrySeconds, setRetrySeconds] = useState(0)

  const pendingIntent = useRef<ContributionIntent | null>(null)

  const isTreasurer = ['treasurer', 'chairperson'].includes(
    stokvel.role.toLowerCase(),
  )

  const cycle = stokvel.currentCycle
  const isOpen = cycle?.status.toLowerCase() === 'open'

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      userId: '',
      amount: '',
    },
  })

  useEffect(() => {
    if (me.data && !form.getValues('userId')) {
      form.setValue('userId', me.data.id)
    }
  }, [me.data, form])

  useEffect(() => {
    if (retrySeconds <= 0) return

    const timeout = window.setTimeout(() => {
      setRetrySeconds((previous) => Math.max(0, previous - 1))
    }, 1000)

    return () => window.clearTimeout(timeout)
  }, [retrySeconds])

  const allMembers =
    members.data?.pages.flatMap((page) => page.items) ?? []

  function invalidatePaymentIntent() {
    pendingIntent.current = null
    setSuccess(false)
    mutation.reset()
  }

  async function onSubmit(values: FormValues) {
    if (!cycle || !isOpen || mutation.isPending || retrySeconds > 0) {
      return
    }

    if (!me.data) return

    if (!isTreasurer && values.userId !== me.data.id) return

    const amountInCents = parseRandsToCents(values.amount)

    const intentFingerprint = JSON.stringify({
      stokvelId: stokvel.stokvelId,
      cycleId: cycle.cycleId,
      userId: values.userId,
      amountInCents,
    })

    const intent = getContributionIntent(
      pendingIntent.current,
      intentFingerprint,
    )

    pendingIntent.current = intent
    setSuccess(false)

    try {
      await mutation.mutateAsync({
        stokvelId: stokvel.stokvelId,
        cycleId: cycle.cycleId,
        userId: values.userId,
        amountInCents,
        idempotencyKey: intent.key,
      })

      pendingIntent.current = null
      setSuccess(true)
      setShowForm(false)
      setRetrySeconds(0)

      form.reset({
        userId: me.data.id,
        amount: '',
      })
    } catch (error) {
      if (error instanceof ApiError && error.status === 429) {
        setRetrySeconds(error.retryAfter ?? 30)
      }

      // Keep the same idempotency key for a safe retry.
    }
  }

  if (!cycle || !isOpen) {
    return null
  }

  return (
    <div className="space-y-3">
      {!showForm && (
        <button
          type="button"
          onClick={() => {
            setShowForm(true)
            setSuccess(false)
          }}
          className="w-full rounded-md bg-primary px-4 py-2 text-primary-foreground"
        >
          Record contribution
        </button>
      )}

      {success && (
        <p role="status" className="text-sm text-green-700">
          Contribution recorded successfully.
        </p>
      )}

      {showForm && (
        <form
          onSubmit={(event) => {
            void form.handleSubmit(onSubmit)(event)
          }}
          className="space-y-4 rounded-lg border p-4"
        >
          <h3 className="font-semibold">
            Record contribution - {cycle.period}
          </h3>

          {isTreasurer ? (
            <div className="space-y-2">
              <label htmlFor="record-member">Member</label>

              <select
  id="record-member"
  {...form.register('userId')}
  onChange={(event) => {
    form.register('userId').onChange(event)
    invalidatePaymentIntent()
  }}
  className="w-full rounded-md border bg-background p-2"
>
                <option value="">Select a member</option>

                {allMembers.map((member) => (
                  <option key={member.userId} value={member.userId}>
                    {member.name}
                  </option>
                ))}
              </select>

              {members.hasNextPage && (
                <button
                  type="button"
                  disabled={members.isFetchingNextPage}
                  onClick={() => void members.fetchNextPage()}
                  className="text-sm underline"
                >
                  Load more members
                </button>
              )}
            </div>
          ) : (
            <p className="text-sm">
              Recording a contribution for yourself.
            </p>
          )}

          {form.formState.errors.userId && (
            <p role="alert" className="text-sm text-red-600">
              {form.formState.errors.userId.message}
            </p>
          )}

          <div className="space-y-2">
            <label htmlFor="record-amount">
              Amount (R)
            </label>

            <input
  id="record-amount"
  type="text"
  inputMode="decimal"
  placeholder="150.50"
  {...form.register('amount')}
  onChange={(event) => {
    form.register('amount').onChange(event)
    invalidatePaymentIntent()
  }}
  className="w-full rounded-md border bg-background p-2"
/>

            {form.formState.errors.amount && (
              <p role="alert" className="text-sm text-red-600">
                {form.formState.errors.amount.message}
              </p>
            )}
          </div>

          {mutation.isError && (
            <div role="alert" className="space-y-1 text-sm text-red-600">
              {mutation.error instanceof ApiError &&
              mutation.error.status === 409 ? (
                <p>
                  This contribution may already have been recorded.
                  Check the contribution history before submitting again.
                </p>
              ) : mutation.error instanceof ApiError &&
                mutation.error.status === 422 ? (
                <p>
                  The contribution details were rejected.
                  Check the selected member and amount.
                </p>
              ) : (
                <p>{mutation.error.message}</p>
              )}

              {mutation.error instanceof ApiError &&
                mutation.error.correlationId && (
                  <p>
                    Reference: {mutation.error.correlationId}
                  </p>
                )}
            </div>
          )}

          {retrySeconds > 0 && (
            <p role="status" className="text-sm text-amber-700">
              Please wait {retrySeconds} seconds before retrying.
            </p>
          )}

          <div className="flex gap-3">
            <button
              type="submit"
              disabled={
                mutation.isPending ||
                !me.data ||
                retrySeconds > 0
              }
              className="rounded-md bg-primary px-4 py-2 text-primary-foreground disabled:opacity-50"
            >
              {mutation.isPending
                ? 'Recording...'
                : retrySeconds > 0
                  ? `Wait ${retrySeconds}s`
                  : 'Submit contribution'}
            </button>

            <button
              type="button"
              disabled={mutation.isPending}
              onClick={() => {
                setShowForm(false)
                invalidatePaymentIntent()
              }}
              className="rounded-md border px-4 py-2"
            >
              Cancel
            </button>
          </div>
        </form>
      )}
    </div>
  )
}