import { NextRequest, NextResponse } from "next/server";
import { getSession } from "@/lib/session";

const API_BASE_URL =
  process.env.API_BASE_URL ?? "http://localhost:5056";

type RouteContext = {
  params: Promise<{ id: string }>;
};

export async function GET(
  request: NextRequest,
  context: RouteContext,
) {
  const session = await getSession();

  if (!session) {
    return NextResponse.json(
      { error: "Unauthorized" },
      { status: 401 },
    );
  }

  const { id } = await context.params;

  if (
    !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)
  ) {
    return NextResponse.json(
      { error: "Invalid stokvel ID" },
      { status: 400 },
    );
  }

  const allowedParameters = [
    "member",
    "from",
    "to",
    "sort",
    "pageSize",
    "pageToken",
  ];

  const parameters = new URLSearchParams();

  for (const key of allowedParameters) {
    const value = request.nextUrl.searchParams.get(key);

    if (value !== null) {
      parameters.set(key, value);
    }
  }

  const url =
    `${API_BASE_URL}/api/v1/stokvels/${encodeURIComponent(id)}` +
    `/contributions?${parameters.toString()}`;

  try {
    const response = await fetch(url, {
      method: "GET",
      headers: {
        Authorization: `Bearer ${session.accessToken}`,
        Accept: "application/json",
      },
      cache: "no-store",
    });

    if (!response.ok) {
      return NextResponse.json(
        { error: "Unable to retrieve contribution history" },
        { status: response.status },
      );
    }

    const data: unknown = await response.json();

    return NextResponse.json(data, {
      headers: {
        "Cache-Control": "private, no-store",
      },
    });
  } catch {
    return NextResponse.json(
      { error: "Contribution service unavailable" },
      { status: 502 },
    );
  }
}
