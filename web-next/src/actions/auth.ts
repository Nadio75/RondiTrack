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
export type RegisterState = {
  error: string | null;
  correlationId: string | null;
  fieldErrors: Record<string, string>;
};

export async function registerAction(
  _previousState: RegisterState,
  formData: FormData,
): Promise<RegisterState> {
  const name = String(formData.get("name") ?? "").trim();
  const contactNumber = String(formData.get("contactNumber") ?? "").trim();
  const email = String(formData.get("email") ?? "").trim();
  const password = String(formData.get("password") ?? "");

  const fieldErrors: Record<string, string> = {};

  if (!name || name.length > 100) {
    fieldErrors.name = "Name must be between 1 and 100 characters.";
  }

  if (!/^\d{10}$/.test(contactNumber)) {
    fieldErrors.contactNumber = "Contact number must be exactly 10 digits.";
  }

  if (!email || email.length > 320 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    fieldErrors.email = "Enter a valid email address.";
  }

  if (password.length < 12) {
    fieldErrors.password = "Password must be at least 12 characters.";
  }

  if (Object.keys(fieldErrors).length > 0) {
    return {
      error: "Please correct the highlighted fields.",
      correlationId: null,
      fieldErrors,
    };
  }

  try {
    const response = await fetch(`${apiBaseUrl}/api/v1/auth/register`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Accept: "application/json",
      },
      body: JSON.stringify({ name, contactNumber, email, password }),
      cache: "no-store",
    });

    if (!response.ok) {
      throw await createApiError(response);
    }
  } catch (error) {
    if (error instanceof ApiError) {
      return {
        error: error.message,
        correlationId: error.correlationId,
        fieldErrors: {},
      };
    }

    return {
      error: "Unable to register. Please try again.",
      correlationId: null,
      fieldErrors: {},
    };
  }

  redirect("/login?registered=1");
}
