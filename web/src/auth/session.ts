
/**
 * Access tokens are kept only in memory.
 *
 * Single-flight refresh means concurrent requests share one
 * refresh promise rather than rotating the cookie repeatedly.
 *
 * RondiTrack rotates refresh cookies and can revoke a token
 * family when an already-used refresh token is presented.
 */

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? "";

let accessToken: string | null = null;
let inFlightRefresh: Promise<string | null> | null = null;
let sessionVersion = 0;
type TokenResponse = {
  accessToken: string;
  tokenType: string;
  expiresIn: number;
};

export class SessionExpiredError extends Error {
  constructor() {
    super("Your session has expired. Please sign in again.");
    this.name = "SessionExpiredError";
  }
}

export function getAccessToken(): string | null {
  return accessToken;
}

function clearSession(): void {
  sessionVersion++;
  accessToken = null;
}

function apiUrl(path: string): string {
  return `${API_BASE_URL.replace(/\/$/, "")}${path}`;
}

export async function login(
  email: string,
  password: string,
): Promise<void> {
  clearSession();

  const response = await fetch(apiUrl("/api/v1/auth/login"), {
    method: "POST",
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ email, password }),
  });

  if (!response.ok) {
    throw new Error(
      response.status === 401
        ? "The email address or password is incorrect."
        : "Unable to sign in. Please try again.",
    );
  }

  const data = (await response.json()) as TokenResponse;
  accessToken = data.accessToken;
}


async function doRefresh(): Promise<string | null> {
  const versionAtStart = sessionVersion;

  try {
    const response = await fetch(apiUrl("/api/v1/auth/refresh"), {
      method: "POST",
      credentials: "include",
    });

    if (versionAtStart !== sessionVersion) {
      return null;
    }

    if (!response.ok) {
      clearSession();
      return null;
    }

    const data = (await response.json()) as TokenResponse;

    if (versionAtStart !== sessionVersion) {
      return null;
    }

    accessToken = data.accessToken;
    return accessToken;
  } catch {
    if (versionAtStart === sessionVersion) {
      clearSession();
    }

    return null;
  }
}

export function refreshAccessToken(): Promise<string | null> {
  if (inFlightRefresh) {
    return inFlightRefresh;
  }

  const refreshPromise = doRefresh();
  inFlightRefresh = refreshPromise;

  void refreshPromise.finally(() => {
    if (inFlightRefresh === refreshPromise) {
      inFlightRefresh = null;
    }
  });

  return refreshPromise;
}

export async function logout(): Promise<void> {
  clearSession();

  try {
    await fetch(apiUrl("/api/v1/auth/logout"), {
      method: "POST",
      credentials: "include",
    });
  } catch {
    // The browser must remain signed out even if offline.
  }
}

/**
 * Authenticated fetch adapter for openapi-fetch.
 * A request is retried at most once after a 401.
 */
export async function authFetch(
  input: RequestInfo | URL,
  init?: RequestInit,
): Promise<Response> {
const request = new Request(input, {
  ...init,
  credentials: "include",
});  const retryableRequest = request.clone();

  let token = accessToken;

  if (!token) {
    token = await refreshAccessToken();
    if (!token) {
      throw new SessionExpiredError();
    }
  }

  const firstRequest = request.clone();
  firstRequest.headers.set("Authorization", `Bearer ${token}`);

  const response = await fetch(firstRequest);

  if (response.status !== 401) {
    return response;
  }

  // Another request may already have refreshed the token.
  const nextToken =
    accessToken && accessToken !== token
      ? accessToken
      : await refreshAccessToken();

  if (!nextToken) {
    throw new SessionExpiredError();
  }

  retryableRequest.headers.set(
    "Authorization",
    `Bearer ${nextToken}`,
  );

  const retryResponse = await fetch(retryableRequest);

  if (retryResponse.status === 401) {
    clearSession();
    throw new SessionExpiredError();
  }

  return retryResponse;
}
