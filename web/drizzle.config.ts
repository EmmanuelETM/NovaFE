import { defineConfig } from "drizzle-kit";

/**
 * Drizzle Kit gestiona **solo** el schema `auth` (tablas de Better Auth).
 * El schema `public` es de EF Core (migraciones de la API .NET) — `schemaFilter`
 * evita que Drizzle lo vea y proponga borrarlo.
 */
export default defineConfig({
  dialect: "postgresql",
  schema: "./lib/auth/schema.ts",
  out: "./drizzle",
  schemaFilter: ["auth"],
  dbCredentials: {
    url: process.env.DATABASE_URL ?? "",
  },
});
