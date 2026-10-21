
import { QueryClient } from '@tanstack/react-query'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      retry: (failureCount, error) => {
        const status =
          typeof error === 'object' &&
          error !== null &&
          'status' in error
            ? Number(error.status)
            : undefined

        if (status !== undefined && status >= 400 && status < 500) {
          return false
        }

        return failureCount < 2
      },
    },
  },
})
