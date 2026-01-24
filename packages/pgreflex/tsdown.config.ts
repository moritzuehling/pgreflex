import { defineConfig } from "tsdown";

export default defineConfig({
  exports: true,
  entry: {
    index: "src/index.ts",
    "connection/*": "src/connection/*",
  },
  // ...config options
});
