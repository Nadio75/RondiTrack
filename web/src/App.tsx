
import { useEffect, useState } from 'react'
import { StokvelCard } from '@/components/StokvelCard'
import { LoginForm } from '@/components/LoginForm'
import { ThemeToggle } from '@/theme/ThemeToggle'
import { useHome } from '@/api/queries'
import { queryClient } from '@/api/queryClient'
import {
  getAccessToken,
  logout,
  refreshAccessToken,
  SessionExpiredError,
} from '@/auth/session'
import { CreateStokvelForm } from '@/components/CreateStokvelForm'
import { DashboardSkeleton } from '@/components/DashboardSkeleton'
import './App.css'

function Dashboard({ onSignedOut }: { onSignedOut: () => void }) {
  const home = useHome()
  const [showCreateForm, setShowCreateForm] = useState(false)

  useEffect(() => {
    if (home.error instanceof SessionExpiredError) {
      onSignedOut()
    }
  }, [home.error, onSignedOut])

 if (home.isPending) {
  return <DashboardSkeleton />
}

  if (home.isError) {
    if (home.error instanceof SessionExpiredError) {
      return null
    }

    return (
      <div role="alert" className="space-y-3">
<p>
  {home.error instanceof Error
    ? home.error.message
    : "We couldn't load your dashboard."}
</p>
        <button
          type="button"
          onClick={() => void home.refetch()}
          className="rounded-md border px-4 py-2"
        >
          Try again
        </button>
      </div>
    )
  }

  const stokvels = home.data.stokvels

  return (
  <section aria-labelledby="stokvels-heading">
    <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
      <h2
        id="stokvels-heading"
        className="text-2xl font-semibold"
      >
        My Stokvels
      </h2>

      {!showCreateForm && (
        <button
          type="button"
          onClick={() => setShowCreateForm(true)}
          className="rounded-md bg-primary px-4 py-2 text-primary-foreground"
        >
          + Create Stokvel
        </button>
      )}
    </div>

    {showCreateForm && (
      <div className="mb-6">
        <CreateStokvelForm
          onCancel={() => setShowCreateForm(false)}
          onCreated={() => setShowCreateForm(false)}
        />
      </div>
    )}

    {stokvels.length === 0 ? (
      <p>
        You don't belong to any stokvels yet.
        Create your first stokvel to get started.
      </p>
    ) : (
      <div className="card-grid">
        {stokvels.map((stokvel) => (
          <StokvelCard
            key={stokvel.stokvelId}
            stokvel={stokvel}
          />
        ))}
      </div>
    )}
  </section>
)
}

function App() {
  const [authState, setAuthState] = useState<
    'checking' | 'signed-in' | 'signed-out'
  >('checking')

  useEffect(() => {
    let active = true

    async function restoreSession() {
      const token = getAccessToken() ?? await refreshAccessToken()

      if (active) {
        setAuthState(token ? 'signed-in' : 'signed-out')
      }
    }

    void restoreSession()

    return () => {
      active = false
    }
  }, [])

  function handleSignedOut() {
    queryClient.clear()
    setAuthState('signed-out')
  }

  async function handleLogout() {
    queryClient.clear()
    setAuthState('signed-out')
    await logout()
  }

  function handleLogin() {
    queryClient.clear()
    setAuthState('signed-in')
  }

  return (
    <main className="app-container space-y-10">
      <header className="page-header">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h1 className="text-3xl font-bold">
              RondiTrack Dashboard
            </h1>
            <p className="mt-2 text-muted-foreground">
              Assignment 7.3 — Live RondiTrack Data
            </p>
          </div>

          <div className="flex items-center gap-3">
            {authState === 'signed-in' && (
              <button
                type="button"
                onClick={() => void handleLogout()}
                className="rounded-md border px-4 py-2"
              >
                Log out
              </button>
            )}
            <ThemeToggle />
          </div>
        </div>
      </header>

      {authState === 'checking' ? (
        <p role="status">Checking your session...</p>
      ) : authState === 'signed-out' ? (
        <LoginForm onLogin={handleLogin} />
      ) : (
        <Dashboard onSignedOut={handleSignedOut} />
      )}
    </main>
  )
}

export default App
