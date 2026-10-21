import { useState, type FormEvent } from 'react'
import { useCreateStokvel } from '@/api/queries'

type Props = {
  onCancel: () => void
  onCreated: () => void
}

export function CreateStokvelForm({
  onCancel,
  onCreated,
}: Props) {
  const [name, setName] = useState('')
  const [amount, setAmount] = useState('500')
  const createStokvel = useCreateStokvel()

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const contributionAmount = Number(amount)
    const trimmedName = name.trim()

    if (
      trimmedName.length === 0 ||
      trimmedName.length > 100 ||
      !Number.isFinite(contributionAmount) ||
      contributionAmount <= 0
    ) {
      return
    }

    try {
      await createStokvel.mutateAsync({
        name: trimmedName,
        contributionAmount,
      })

      onCreated()
    } catch {
      // The mutation error is displayed below.
    }
  }

  return (
    <form
      onSubmit={(event) => void handleSubmit(event)}
      className="space-y-4 rounded-lg border p-5"
    >
      <h3 className="text-xl font-semibold">
        Create a New Stokvel
      </h3>

      <div className="space-y-2">
        <label htmlFor="stokvel-name" className="block font-medium">
          Stokvel name
        </label>

        <input
          id="stokvel-name"
          type="text"
          value={name}
          onChange={(event) => setName(event.target.value)}
          maxLength={100}
          required
          disabled={createStokvel.isPending}
          placeholder="e.g. Ubuntu Savings Club"
          className="w-full rounded-md border bg-background px-3 py-2"
        />
      </div>

      <div className="space-y-2">
        <label htmlFor="contribution-amount" className="block font-medium">
          Contribution amount (R)
        </label>

        <input
          id="contribution-amount"
          type="number"
          min="0.01"
          step="0.01"
          value={amount}
          onChange={(event) => setAmount(event.target.value)}
          required
          disabled={createStokvel.isPending}
          className="w-full rounded-md border bg-background px-3 py-2"
        />
      </div>

      {createStokvel.isError && (
        <p role="alert" className="text-sm text-red-600">
          {createStokvel.error.message}
        </p>
      )}

      <div className="flex gap-3">
        <button
          type="submit"
          disabled={createStokvel.isPending}
          className="rounded-md bg-primary px-4 py-2 text-primary-foreground disabled:opacity-50"
        >
          {createStokvel.isPending
            ? 'Creating...'
            : 'Create Stokvel'}
        </button>

        <button
          type="button"
          onClick={onCancel}
          disabled={createStokvel.isPending}
          className="rounded-md border px-4 py-2"
        >
          Cancel
        </button>
      </div>
    </form>
  )
}