
import { Monitor, Moon, Sun } from 'lucide-react'

import {
  useTheme,
  type ThemePreference,
} from './ThemeProvider'

const options = [
  {
    value: 'light',
    label: 'Light',
    Icon: Sun,
  },
  {
    value: 'dark',
    label: 'Dark',
    Icon: Moon,
  },
  {
    value: 'system',
    label: 'System',
    Icon: Monitor,
  },
] as const

export function ThemeToggle() {
  const { theme, setTheme } = useTheme()

  return (
    <div
      role="group"
      aria-label="Appearance preference"
      className="inline-flex max-w-full flex-wrap gap-1 rounded-xl border border-border bg-card p-1"
    >
      {options.map((option) => {
        const selected = theme === option.value
        const Icon = option.Icon

        return (
          <button
            key={option.value}
            type="button"
            aria-pressed={selected}
            onClick={() =>
              setTheme(
                option.value as ThemePreference,
              )
            }
            className={[
              'inline-flex min-h-10 items-center justify-center gap-2',
              'rounded-lg px-3 py-2 text-sm font-medium',
              'transition-colors focus-visible:outline-2',
              'focus-visible:outline-offset-2 focus-visible:outline-ring',
              selected
                ? 'bg-primary text-primary-foreground'
                : 'text-foreground hover:bg-muted',
            ].join(' ')}
          >
            <Icon aria-hidden="true" className="size-4" />
            {option.label}
          </button>
        )
      })}
    </div>
  )
}
