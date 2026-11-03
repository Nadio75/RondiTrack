import { NextRequest, NextResponse } from "next/server";
import { apiBaseUrl } from "@/lib/api";
import { getSession } from "@/lib/session";

const guidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

type RouteContext = {
  params: Promise<{ id: string; contributionId: string }>;
};

export async function GET(
  _request: NextRequest,
  context: RouteContext,
) {
  const { id, contributionId } = await context.params;

  if (!guidPattern.test(id) || !guidPattern.test(contributionId)) {
    return new NextResponse("Not found", { status: 404 });
  }

  const session = await getSession();

  if (!session) {
    return new NextResponse("Authentication required", { status: 401 });
  }

  try {
    const response = await fetch(
      `${apiBaseUrl}/api/v1/stokvels/${encodeURIComponent(id)}` +
        `/contributions/${encodeURIComponent(contributionId)}/proof`,
      {
        headers: {
          Authorization: `Bearer ${session.accessToken}`,
        },
        cache: "no-store",
      },
    );

    if (!response.ok || !response.body) {
      return new NextResponse(
        response.status === 403 || response.status === 404
          ? "Not found"
          : "Unable to download receipt",
        {
          status: response.status === 403 ? 404 : response.status,
          headers: { "Cache-Control": "private, no-store" },
        },
      );
    }

    return new NextResponse(response.body, {
      status: 200,
      headers: {
        "Content-Type":
          response.headers.get("Content-Type") ?? "application/octet-stream",
        "Content-Disposition":
          response.headers.get("Content-Disposition") ??
          'attachment; filename="receipt"',
        "Cache-Control": "private, no-store",
        "X-Content-Type-Options": "nosniff",
      },
    });
  } catch {
    return new NextResponse("Unable to download receipt", {
      status: 502,
      headers: { "Cache-Control": "private, no-store" },
    });
  }
}
