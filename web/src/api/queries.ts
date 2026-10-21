
import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/react-query'
import { api } from './client'
import { createApiError } from './ApiError'


async function unwrap<T>(
  result: {
    data?: T
    error?: unknown
    response: Response
  },
): Promise<T> {
  if (!result.response.ok) {
    throw await createApiError(result.response)
  }

  if (result.data === undefined) {
    throw new Error('The server returned no data.')
  }

  return result.data
}
export function useMe() {
  return useQuery({
    queryKey: ['me'],
    queryFn: async () =>
      unwrap(await api.GET('/api/v1/me')),
  })
}

export function useHome() {
  return useQuery({
    queryKey: ['home'],
    queryFn: async () =>
      unwrap(await api.GET('/api/v1/me/home')),
  })
}

export function useStokvels() {
  return useQuery({
    queryKey: ['stokvels'],
    queryFn: async () =>
      unwrap(await api.GET('/api/v1/stokvels')),
  })
}

export function useStokvel(id: string | null) {
  return useQuery({
    queryKey: ['stokvel', id],
    enabled: Boolean(id),
    queryFn: async () =>
      unwrap(
        await api.GET('/api/v1/stokvels/{id}', {
          params: {
            path: { id: id! },
          },
        }),
      ),
  })
}

export function useCycles(stokvelId: string | null) {
  return useQuery({
    queryKey: ['cycles', stokvelId],
    enabled: Boolean(stokvelId),
    queryFn: async () =>
      unwrap(
        await api.GET('/api/v1/stokvels/{stokvelId}/cycles', {
          params: {
            path: { stokvelId: stokvelId! },
          },
        }),
      ),
  })
}


export function useContributions(
  stokvelId: string | null,
  cycleId: string | null,
) {
  return useInfiniteQuery({
    queryKey: ['contributions', stokvelId, cycleId],

    enabled: Boolean(stokvelId && cycleId),

    initialPageParam: '' as string,

    queryFn: async ({ pageParam }) => {
      return unwrap(
        await api.GET(
          '/api/v1/stokvels/{id}/cycles/{cycleId}/contributions',
          {
            params: {
              path: {
                id: stokvelId!,
                cycleId: cycleId!,
              },
              query: {
                page_size: 20,
                ...(pageParam
                  ? { page_token: pageParam }
                  : {}),
              },
            },
          },
        ),
      )
    },

    getNextPageParam: (lastPage) => {
      const token = lastPage.nextPageToken

      return token && token.length > 0
        ? token
        : undefined
    },
  })
}

export function useCreateStokvel() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (request: {
      name: string
      contributionAmount: number
    }) => {
      const result = await api.POST('/api/v1/stokvels', {
        body: request,
      })

      return unwrap(result)
    },

    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['home'] }),
        queryClient.invalidateQueries({ queryKey: ['stokvels'] }),
      ])
    },
  })
}
