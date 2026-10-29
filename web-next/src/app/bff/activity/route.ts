import { NextRequest, NextResponse } from "next/server";
import { getSession } from "@/lib/session";

const apiBaseUrl =
  process.env.API_BASE_URL ?? "http://localhost:5056";

export async function GET(request: NextRequest) {
  const session = await getSession();

  if (!session) {
    return NextResponse.json(
      { error: "Unauthorized" },
      {
        status: 401,
        headers: { "Cache-Control": "no-store" },
      },
    );
  }

  const incoming = request.nextUrl.searchParams;
  const params = new URLSearchParams();

  const pageSize = incoming.get("pageSize");
  const pageToken = incoming.get("pageToken");

  if (pageSize !== null) {
    const size = Number(pageSize);

    if (
      !Number.isInteger(size) ||
      size < 1 ||
      size > 100
    ) {
      return NextResponse.json(
        { error: "pageSize must be between 1 and 100" },
        { status: 400 },
      );
    }

    params.set("pageSize", String(size));
  }

  if (pageToken) {
    params.set("pageToken", pageToken);
  }

  const query = params.toString();
  const url =
    `${apiBaseUrl}/api/v1/activity` +
    (query ? `?${query}` : "");

  try {
    const response = await fetch(url, {
      method: "GET",
      headers: {
        Authorization: `Bearer ${session.accessToken}`,
        Accept: "application/json",
      },
      cache: "no-store",
    });

    const body = await response.text();

    return new NextResponse(body, {
      status: response.status,
      headers: {
        "Content-Type":
          response.headers.get("content-type") ??
          "application/json",
        "Cache-Control": "no-store",
      },
    });
  } catch {
    return NextResponse.json(
      { error: "Activity service is unavailable" },
      {
        status: 502,
        headers: { "Cache-Control": "no-store" },
      },
    );
  }
}
