import { defineConfig } from "vitest/config";

export default defineConfig({
  build: {
    lib: {
      entry: { highlight: "src/highlight.ts", toc: "src/toc.ts" },
      formats: ["es"],
      fileName: (_format, name) => `${name}.js`,
    },
    outDir: "../src/Loonaciaverse.Web/wwwroot/js",
    emptyOutDir: true,
  },
  test: {
    environment: "happy-dom",
  },
});
