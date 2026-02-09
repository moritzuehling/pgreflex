import "@types/bun";
import { defineConfig } from "drizzle-kit";

export default defineConfig({
  dialect: "postgresql",
  schema: "./server/schema.ts",
  dbCredentials: {
    url: process.env.DATABASE_URL!,
  },
});
