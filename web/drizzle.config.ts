import { loadEnvConfig } from "@next/env";
import { defineConfig } from "drizzle-kit";

// drizzle-kit corre fuera de Next y no lee `.env.local` por su cuenta. `@next/env`
// (dependencia de `next`) aplica la misma precedencia de archivos que `bun dev`.
loadEnvConfig(process.cwd());

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
