export function DashboardSkeleton() {
  return (
    <section
      aria-label="Loading dashboard"
      aria-busy="true"
      role="status"
      className="space-y-6"
    >
      <span className="sr-only">
        Loading your dashboard...
      </span>

      <div className="flex items-center justify-between gap-4">
        <div className="h-8 w-40 animate-pulse rounded bg-muted" />
        <div className="h-10 w-36 animate-pulse rounded bg-muted" />
      </div>

      <div className="card-grid">
        {[1, 2].map((item) => (
          <div
            key={item}
            className="space-y-5 rounded-2xl border p-6"
          >
            <div className="flex justify-between gap-4">
              <div className="h-7 w-36 animate-pulse rounded bg-muted" />
              <div className="h-7 w-24 animate-pulse rounded-full bg-muted" />
            </div>

            <div className="space-y-3">
              <div className="h-4 w-32 animate-pulse rounded bg-muted" />
              <div className="h-8 w-44 animate-pulse rounded bg-muted" />
            </div>

            <div className="h-4 w-52 animate-pulse rounded bg-muted" />
          </div>
        ))}
      </div>
    </section>
  )
}