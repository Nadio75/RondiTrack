"use client";

import { useActionState } from "react";
import Link from "next/link";
import { registerAction, type RegisterState } from "@/actions/auth";

const initialState: RegisterState = {
  error: null,
  correlationId: null,
  fieldErrors: {},
};

export default function RegisterForm() {
  const [state, formAction, pending] = useActionState(
    registerAction,
    initialState,
  );

  return (
    <form
      action={formAction}
      className="mx-auto mt-16 max-w-md space-y-5 rounded-xl border p-8"
    >
      <h1 className="text-2xl font-bold">Create your RondiTrack account</h1>

      <div className="space-y-2">
        <label htmlFor="name" className="block">Full name</label>
        <input
          id="name"
          name="name"
          type="text"
          autoComplete="name"
          maxLength={100}
          required
          className="w-full rounded-md border p-3 text-black"
        />
        {state.fieldErrors.name && (
          <p className="text-sm text-red-600">{state.fieldErrors.name}</p>
        )}
      </div>

      <div className="space-y-2">
        <label htmlFor="contactNumber" className="block">Contact number</label>
        <input
          id="contactNumber"
          name="contactNumber"
          type="tel"
          autoComplete="tel"
          inputMode="numeric"
          pattern="[0-9]{10}"
          maxLength={10}
          required
          className="w-full rounded-md border p-3 text-black"
        />
        {state.fieldErrors.contactNumber && (
          <p className="text-sm text-red-600">
            {state.fieldErrors.contactNumber}
          </p>
        )}
      </div>

      <div className="space-y-2">
        <label htmlFor="email" className="block">Email address</label>
        <input
          id="email"
          name="email"
          type="email"
          autoComplete="username"
          maxLength={320}
          required
          className="w-full rounded-md border p-3 text-black"
        />
        {state.fieldErrors.email && (
          <p className="text-sm text-red-600">{state.fieldErrors.email}</p>
        )}
      </div>

      <div className="space-y-2">
        <label htmlFor="password" className="block">Password</label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="new-password"
          minLength={12}
          required
          className="w-full rounded-md border p-3 text-black"
        />
        {state.fieldErrors.password && (
          <p className="text-sm text-red-600">{state.fieldErrors.password}</p>
        )}
      </div>

      {state.error && (
        <div role="alert" className="text-sm text-red-600">
          <p>{state.error}</p>
          {state.correlationId && (
            <p>Reference: {state.correlationId}</p>
          )}
        </div>
      )}

      <button
        type="submit"
        disabled={pending}
        className="w-full rounded-md bg-blue-700 p-3 font-semibold text-white disabled:opacity-50"
      >
        {pending ? "Creating account..." : "Create account"}
      </button>

      <p className="text-center text-sm">
        Already have an account?{" "}
        <Link href="/login" className="text-blue-600 underline">
          Sign in
        </Link>
      </p>
    </form>
  );
}
