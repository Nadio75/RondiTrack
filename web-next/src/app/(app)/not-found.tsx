import Link from "next/link";

export default function NotFoundPage() {
  return (
    <section className="space-y-4">
      <h1 className="text-3xl font-bold">Stokvel not found</h1>
      <p>
        This stokvel does not exist, or you do not have access to it.
      </p>
      <Link href="/stokvels" className="text-blue-700 underline">
        Back to my stokvels
      </Link>
    </section>
  );
}
