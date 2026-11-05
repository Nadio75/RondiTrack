import { fileURLToPath, URL } from "node:url";
import { defineConfig, configDefaults } from "vitest/config";

export default defineConfig({
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  test: {
    environment: "node",
    setupFiles: ["./src/test/setup.ts"],
    restoreMocks: true,
    clearMocks: true,
    exclude: [
      ...configDefaults.exclude,
      "**/e2e/**",
    ],
  },
});
