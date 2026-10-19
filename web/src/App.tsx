
import { StokvelCard } from './components/StokvelCard'
import { ContributionCard } from './components/ContributionCard'

import {
  sampleDashboard,
  sampleContributions,
} from './data/sample'

import './App.css'

function App() {
  return (
    <main className="app-container">
      <header className="page-header">
        <h1>RondiTrack Dashboard</h1>
        <p>Assignment 7.1 — React 19 Components</p>
        <p className="demo-notice">
          Demonstration data only. No live API requests.
        </p>
      </header>

      <section aria-labelledby="stokvels-heading">
        <h2 id="stokvels-heading">My Stokvels</h2>

        <div className="card-grid">
          {sampleDashboard.stokvels.map((stokvel) => (
            <StokvelCard
              key={stokvel.stokvelId}
              stokvel={stokvel}
            />
          ))}
        </div>
      </section>

      <section aria-labelledby="contributions-heading">
        <h2 id="contributions-heading">
          Sample Contributions
        </h2>

        <div className="card-grid">
          {sampleContributions.map((item) => (
            <ContributionCard
              key={item.contribution.id}
              contribution={item.contribution}
              demoStatus={item.demoStatus}
            />
          ))}
        </div>
      </section>
    </main>
  )
}

export default App
