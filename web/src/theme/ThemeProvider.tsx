
import {
  createContext,
  useContext,
  useEffect,
  useState,
} from 'react'

import type { ReactNode } from 'react'

export type ThemePreference = 'light' | 'dark' | 'system'

type ThemeContextValue = {
  theme: ThemePreference
  setTheme: (theme: ThemePreference) => void
}

const STORAGE_KEY = 'ronditrack-theme'

const ThemeContext = createContext<ThemeContextValue | null>(
  null,
)

function readSavedTheme(): ThemePreference {
  try {
    const saved = window.localStorage.getItem(STORAGE_KEY)

    if (
      saved === 'light' ||
      saved === 'dark' ||
      saved === 'system'
    ) {
      return saved
    }
  } catch {
    // Continue using the system preference.
  }

  return 'system'
}

export function ThemeProvider({
  children,
}: {
  children: ReactNode
}) {
  const [theme, setTheme] = useState<ThemePreference>(
    readSavedTheme,
  )

  useEffect(() => {
    const media = window.matchMedia(
      '(prefers-color-scheme: dark)',
    )

    function applyTheme() {
      const isDark =
        theme === 'dark' ||
        (theme === 'system' && media.matches)

      document.documentElement.classList.toggle(
        'dark',
        isDark,
      )

      document.documentElement.style.colorScheme =
        isDark ? 'dark' : 'light'

      document.documentElement.style.backgroundColor =
        isDark ? '#241b17' : '#faf7f2'
    }

    applyTheme()

    if (theme === 'system') {
      media.addEventListener('change', applyTheme)
    }

    return () => {
      media.removeEventListener('change', applyTheme)
    }
  }, [theme])

  function updateTheme(nextTheme: ThemePreference) {
    setTheme(nextTheme)

    try {
      window.localStorage.setItem(
        STORAGE_KEY,
        nextTheme,
      )
    } catch {
      // Theme switching still works without storage.
    }
  }

  return (
    <ThemeContext.Provider
      value={{
        theme,
        setTheme: updateTheme,
      }}
    >
      {children}
    </ThemeContext.Provider>
  )
}

export function useTheme() {
  const context = useContext(ThemeContext)

  if (!context) {
    throw new Error(
      'useTheme must be used inside ThemeProvider',
    )
  }

  return context
}
