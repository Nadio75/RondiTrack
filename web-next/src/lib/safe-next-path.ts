export function safeNextPath(value: unknown): string {
  if (typeof value !== "string") return "/stokvels";

  if (
    !value.startsWith("/") ||
    value.startsWith("//") ||
    value.includes("\\") ||
    /[\u0000-\u001F\u007F]/.test(value)
  ) {
    return "/stokvels";
  }

  try {
    const url = new URL(value, "http://localhost");

    if (url.origin !== "http://localhost") {
      return "/stokvels";
    }

    if (url.pathname.startsWith("/login")) {
      return "/stokvels";
    }

    return url.pathname + url.search + url.hash;
  } catch {
    return "/stokvels";
  }
}
