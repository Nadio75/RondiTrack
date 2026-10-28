import { singleFlightRefresh } from "./lib/refresh-single-flight";
import { NextRequest, NextResponse } from "next/server";
import { decodeJwt } from "jose";
import {
  decryptSession,
  encryptSession,
  sessionCookieName,
  sessionCookieOptions,
  type Session,
} from "./lib/session-crypto";

const apiBaseUrl =
  process.env.API_BASE_URL ?? "http://localhost:5056";

const refreshMarginSeconds = Number(
  process.env.AUTH_REFRESH_MARGIN_SECONDS ?? "60",
);

function loginRedirect(request: NextRequest, expired = false) {
  const url = new URL("/login", request.url);

  if (expired) {
    url.searchParams.set("expired", "1");
  } else {
    url.searchParams.set(
      "next",
      request.nextUrl.pathname + request.nextUrl.search,
    );
  }

  return NextResponse.redirect(url);
}

function tokenExpiresSoon(accessToken: string): boolean {
  try {
    const { exp } = decodeJwt(accessToken);

    if (typeof exp !== "number") return true;

    return exp - Math.floor(Date.now() / 1000) <= refreshMarginSeconds;
  } catch {
    return true;
  }
}

function clearAndRedirect(request: NextRequest) {
  const response = loginRedirect(request, true);
  response.cookies.delete(sessionCookieName);
  return response;
}

async function refreshSession(session: Session): Promise<Session | null> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/auth/refresh`, {
      method: "POST",
      headers: {
        Cookie: `ronditrack_refresh=${session.refreshToken}`,
        Accept: "application/json",
      },
      cache: "no-store",
    });

    if (!response.ok) {
      console.warn("[AUTH REFRESH] Failed with HTTP status:", response.status);
      return null;
    }

    console.info("[AUTH REFRESH] Backend accepted refresh request.");

    const result = (await response.json()) as {
      accessToken?: string;
    };

    const refreshCookie = response.headers
      .getSetCookie()
      .find((cookie) => cookie.startsWith("ronditrack_refresh="));

    const refreshToken = refreshCookie
      ?.split(";")[0]
      .substring("ronditrack_refresh=".length);

    if (!result.accessToken || !refreshToken) return null;

    return {
      ...session,
      accessToken: result.accessToken,
      refreshToken,
    };
  } catch {
    return null;
  }
}

export async function proxy(request: NextRequest) {
  const cookieValue = request.cookies.get(sessionCookieName)?.value;
  const session = await decryptSession(cookieValue);

  if (!session) {
    return loginRedirect(request);
  }

  if (!tokenExpiresSoon(session.accessToken)) {
    return NextResponse.next();
  }

  const refreshed = await singleFlightRefresh(session, () => refreshSession(session));

  if (!refreshed) {
    return clearAndRedirect(request);
  }

  const encrypted = await encryptSession(refreshed);

  // Update the current request so Server Components read the new token.
  const requestHeaders = new Headers(request.headers);
  const requestCookies = request.cookies;
  requestCookies.set(sessionCookieName, encrypted);
  requestHeaders.set("cookie", requestCookies.toString());

  const response = NextResponse.next({
    request: {
      headers: requestHeaders,
    },
  });

  // Update the browser cookie for future requests.
  response.cookies.set(
    sessionCookieName,
    encrypted,
    sessionCookieOptions,
  );

  return response;
}

export const config = {
  matcher: [
    "/stokvels/:path*",
    "/dashboard/:path*",
  ],
};
