import { NextResponse } from "next/server";
import { apiBaseUrl } from "@/lib/api";
import { getSession } from "@/lib/session";

type Context = {
  params: Promise<{
    id: string;
    contributionId: string;
  }>;
};

const guidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export async function GET(
  _request: Request,
  context: Context,
) {
  const { id, contributionId } = await context.params;

  if (!guidPattern.test(id) || !guidPattern.test(contributionId)) {
    return new NextResponse("Not found", { status: 404 });
  }

  const session = await getSession();

  if (!session) {
    return new NextResponse("Authentication required", {
      status: 401,
      headers: { "Cache-Control": "private, no-store" },
    });
  }

  try {
    const response = await fetch(
      `${apiBaseUrl}/api/v1/stokvels/${id}` +
        `/contributions/${contributionId}/proof/status`,
      {
        headers: {
          Authorization: `Bearer ${session.accessToken}`,
        },
        cache: "no-store",
      },
    );

    if (!response.ok) {
      return new NextResponse("Unable to load receipt status", {
        status: response.status === 403 ? 404 : response.status,
        headers: { "Cache-Control": "private, no-store" },
      });
    }

    const data = await response.json();

    return NextResponse.json(data, {
      headers: {
        "Cache-Control": "private, no-store",
        "X-Content-Type-Options": "nosniff",
      },
    });
  } catch {
    return new NextResponse("Receipt status unavailable", {
      status: 502,
      headers: { "Cache-Control": "private, no-store" },
    });
  }
}
