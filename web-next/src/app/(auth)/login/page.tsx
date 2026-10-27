import { getSession } from "@/lib/session";
import { redirect } from "next/navigation";
import LoginForm from "./LoginForm";

export default async function LoginPage({
  searchParams,
}: PageProps<"/login">) {
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
