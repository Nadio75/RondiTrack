type ProblemDetails = {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}

const defaultMessages: Record<number, string> = {
  400: 'The information provided is invalid.',
  401: 'Your session has expired. Please sign in again.',
  403: 'You do not have permission to perform this action.',
  404: 'The requested information could not be found.',
  409: 'This action conflicts with existing information.',
  429: 'Too many requests. Please try again later.',
  500: 'The server encountered an error. Please try again.',
  503: 'The service is temporarily unavailable.',
}

export class ApiError extends Error {
  readonly status: number
  readonly retryAfter: number | null

  constructor(
    status: number,
    message: string,
    retryAfter: number | null = null,
  ) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.retryAfter = retryAfter
  }
}

export async function createApiError(
  response: Response,
): Promise<ApiError> {
  const status = response.status
  const retryAfterHeader = response.headers.get('Retry-After')

  let retryAfter: number | null = null

  if (retryAfterHeader) {
    const seconds = Number(retryAfterHeader)

    if (Number.isFinite(seconds) && seconds >= 0) {
      retryAfter = seconds
    } else {
      const date = Date.parse(retryAfterHeader)

      if (!Number.isNaN(date)) {
        retryAfter = Math.max(
          0,
          Math.ceil((date - Date.now()) / 1000),
        )
      }
    }
  }

  let problem: ProblemDetails | null = null

  try {
    problem = (await response.clone().json()) as ProblemDetails
  } catch {
    // The API may return an empty or non-JSON error response.
  }

  const validationMessage = problem?.errors
    ? Object.values(problem.errors)
        .flat()
        .filter((message) => typeof message === 'string')
        .join(' ')
    : ''

  const message =
    validationMessage ||
    problem?.detail ||
    problem?.title ||
    defaultMessages[status] ||
    `Request failed (${status}).`

  return new ApiError(status, message, retryAfter)
}