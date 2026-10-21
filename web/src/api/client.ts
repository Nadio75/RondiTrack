
import createClient from 'openapi-fetch'
import type { paths } from './schema'
import { authFetch } from '@/auth/session'

export const api = createClient<paths>({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? '',
  fetch: authFetch,
})
