"use client";

import { useActionState } from "react";
import { loginAction, type LoginState } from "@/actions/auth";

const initialState: LoginState = {
  error: null,
  correlationId: null,
};

export default function LoginForm() {
  const [state, formAction, pending] = useActionState(
    loginAction,
    initialState,
  );

  return (
    <form
      action={formAction}
      className="mx-auto mt-16 max-w-md space-y-5 rounded-xl border p-8"
    >
      <h1 className="text-2xl font-bold">Sign in to RondiTrack</h1>

      <div className="space-y-2">
        <label htmlFor="email" className="block">
          Email
        </label>
        <input
          id="email"
          name="email"
          type="email"
          autoComplete="username"
          required
          className="w-full rounded-md border p-3 text-black"
        />
      </div>

      <div className="space-y-2">
        <label htmlFor="password" className="block">
          Password
        </label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="current-password"
          required
          className="w-full rounded-md border p-3 text-black"
        />
      </div>

      {state.error && (
        <div role="alert" className="text-sm text-red-600">
          <p>{state.error}</p>
          {state.correlationId && (
            <p>Correlation ID: {state.correlationId}</p>
          )}
        </div>
      )}

      <button
        type="submit"
        disabled={pending}
        className="w-full rounded-md bg-blue-700 p-3 font-semibold text-white disabled:opacity-50"
      >
        {pending ? "Signing in..." : "Sign in"}
      </button>
    </form>
  );
}
