import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactCompiler: true,
  cacheComponents: true, // Enables partial prerendering and streaming.
  partialPrefetching: true, // Prefetches the static shell without waiting for dynamic data.
  agentRules: false,

  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
