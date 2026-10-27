"use client";

import { useEffect } from "react";

export default function ErrorPage({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("RondiTrack route error:", error.message);
  }, [error]);

  return (
    <section className="space-y-4 rounded-xl border bg-white p-6">
      <h1 className="text-2xl font-bold">Something went wrong</h1>
      <p>We could not load this information. Please try again.</p>
      <button
        onClick={reset}
        className="rounded bg-blue-700 px-4 py-2 text-white"
      >
        Try again
      </button>
    </section>
  );
}
