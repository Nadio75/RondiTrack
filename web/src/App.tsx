
import { StokvelCard } from '@/components/StokvelCard'
import { ContributionList } from '@/components/ContributionList'
import { ThemeToggle } from '@/theme/ThemeToggle'

import {
  sampleDashboard,
  sampleCycles,
  sampleContributions,
} from '@/data/sample'

import './App.css'

const contributions = sampleContributions.map(
  (item) => item.contribution,
)

const demoStatuses = Object.fromEntries(
  sampleContributions.map((item) => [
    item.contribution.id,
    item.demoStatus,
  ]),
)

function App() {
  return (
    <main className="app-container space-y-10">
      <header className="page-header">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0">
            <h1 className="break-words text-3xl font-bold">
              RondiTrack Dashboard
            </h1>

            <p className="mt-2 text-muted-foreground">
              Assignment 7.2 — Contribution cycles
            </p>

            <p className="demo-notice mt-2">
              Demonstration data only. No live API requests.
            </p>
          </div>

          <ThemeToggle />
        </div>
      </header>

      <section
        aria-labelledby="stokvels-heading"
        className="min-w-0"
      >
        <h2
          id="stokvels-heading"
          className="mb-4 break-words text-2xl font-semibold"
        >
          My Stokvels
        </h2>

        <div className="card-grid">
          {sampleDashboard.stokvels.map((stokvel) => (
            <StokvelCard
              key={stokvel.stokvelId}
              stokvel={stokvel}
            />
          ))}
        </div>
      </section>

      <section
        aria-labelledby="contributions-heading"
        className="min-w-0"
      >
        <h2
          id="contributions-heading"
          className="mb-4 break-words text-2xl font-semibold"
        >
          Contributions by Cycle
        </h2>

        <ContributionList
          cycles={sampleCycles}
          contributions={contributions}
          demoStatuses={demoStatuses}
        />
      </section>
    </main>
  )
}

export default App
