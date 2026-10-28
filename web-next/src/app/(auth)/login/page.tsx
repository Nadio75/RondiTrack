import { Suspense } from "react";
import { getSession } from "@/lib/session";
import { redirect } from "next/navigation";
import LoginForm from "./LoginForm";

async function LoginContent({
  searchParams,
}: {
  searchParams: PageProps<"/login">["searchParams"];
}) {
  const params = await searchParams;
  const expired = params.expired === "1";

  const session = await getSession();

  if (session && !expired) {
    redirect("/stokvels");
  }

  return (
    <main className="min-h-screen p-6">
      <LoginForm />
    </main>
  );
}

export default function LoginPage({
  searchParams,
}: PageProps<"/login">) {
  return (
    <Suspense
      fallback={
        <main className="min-h-screen p-6">
          <p>Loading sign-in page...</p>
        </main>
      }
    >
      <LoginContent searchParams={searchParams} />
    </Suspense>
  );
}