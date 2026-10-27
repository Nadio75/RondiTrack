"use server";

import { redirect } from "next/navigation";
import {
  apiBaseUrl,
  createApiError,
  ApiError,
} from "@/lib/api";
import { deleteSession, setSession } from "@/lib/session";

export type LoginState = {
  error: string | null;
  correlationId: string | null;
};

export async function loginAction(
  _previousState: LoginState,
  formData: FormData,
): Promise<LoginState> {
  const email = String(formData.get("email") ?? "").trim();
  const password = String(formData.get("password") ?? "");

  if (!email || !password) {
    return {
      error: "Email and password are required.",
      correlationId: null,
    };
  }

  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/auth/login`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Accept: "application/json",
      },
      body: JSON.stringify({ email, password }),
      cache: "no-store",
    });

    if (!response.ok) {
      throw await createApiError(response);
    }

    const login = (await response.json()) as {
      accessToken: string;
      tokenType: string;
      expiresIn: number;
    };

    const refreshCookie = response.headers
      .getSetCookie()
      .find((cookie) => cookie.startsWith("ronditrack_refresh="));

    const refreshToken = refreshCookie
      ?.split(";")[0]
      .substring("ronditrack_refresh=".length);

    if (!login.accessToken || !refreshToken) {
      return {
        error: "Login response is missing session credentials.",
        correlationId: null,
      };
    }

    const profileResponse = await fetch(`${apiBaseUrl}/api/v1/me`, {
      headers: {
        Authorization: `Bearer ${login.accessToken}`,
        Accept: "application/json",
      },
      cache: "no-store",
    });

    if (!profileResponse.ok) {
      throw await createApiError(profileResponse);
    }

    const user = (await profileResponse.json()) as {
      id: string;
      name: string;
      email: string;
    };

    await setSession({
      accessToken: login.accessToken,
      refreshToken,
      user,
    });
  } catch (error) {
    if (error instanceof ApiError) {
      return {
        error: error.message,
        correlationId: error.correlationId,
      };
    }

    return {
      error: "Unable to reach the server. Please try again.",
      correlationId: null,
    };
  }

  redirect("/stokvels");
}

export async function logoutAction(): Promise<void> {
  const { getSession } = await import("@/lib/session");
  const session = await getSession();

  if (session?.refreshToken) {
    try {
      await fetch(`${apiBaseUrl}/api/v1/auth/logout`, {
        method: "POST",
        headers: {
          Cookie: `ronditrack_refresh=${session.refreshToken}`,
        },
        cache: "no-store",
      });
    } catch {
      // Local session must still be cleared if API logout fails.
    }
  }

  await deleteSession();
  redirect("/login");
}
