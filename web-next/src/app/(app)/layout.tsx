import { PinnedStokvelProvider } from "@/lib/pinned-stokvels-provider";
import { SecureLogoutButton } from "@/lib/secure-logout-button";
import { PinnedStokvelsSidebar } from "@/lib/pinned-stokvels-sidebar";
import Link from "next/link";
import { Suspense } from "react";
import { redirect } from "next/navigation";
import { getSession } from "@/lib/session";
import { logoutAction } from "@/actions/auth";

async function AuthenticatedShell({
  children,
}: {
  children: React.ReactNode;
}) {
  const session = await getSession();

  if (!session) {
    redirect("/login");
  }

  return (
    <PinnedStokvelProvider userId={session.user.id}>
      <div className="min-h-screen bg-gray-50 text-gray-900 md:flex">
      <aside className="border-b bg-white p-6 md:min-h-screen md:w-64 md:border-b-0 md:border-r">
        <h1 className="mb-6 text-2xl font-bold text-blue-800">
          RondiTrack
        </h1>

        <nav aria-label="Main navigation" className="space-y-3">
          <Link href="/stokvels" className="block hover:underline">
            My Stokvels
          </Link>
          <Link href="/activity" className="block hover:underline">
            My Activity
          </Link>
          <Link href="/dashboard/member" className="block hover:underline">
            Member Dashboard
          </Link>
          <Link href="/dashboard/treasurer" className="block hover:underline">
            Treasurer Dashboard
          </Link>
        </nav>
          <PinnedStokvelsSidebar />

        <div className="mt-10 border-t pt-5">
          <p className="font-semibold">{session.user.name}</p>
          <p className="break-all text-sm text-gray-600">
            {session.user.email}
          </p>

          <form action={logoutAction} className="mt-4">
            <SecureLogoutButton />
          </form>
        </div>
      </aside>

      <main className="w-full flex-1 p-6 md:p-10">
        {children}
      </main>
    </div>
    </PinnedStokvelProvider>
  );
}

export default function AppLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <Suspense
      fallback={
        <div className="min-h-screen p-6">
          <p>Loading your RondiTrack workspace...</p>
        </div>
      }
    >
      <AuthenticatedShell>{children}</AuthenticatedShell>
    </Suspense>
  );
}