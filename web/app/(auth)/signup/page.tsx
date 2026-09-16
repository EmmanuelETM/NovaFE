import type { Metadata } from "next";
import { redirect } from "next/navigation";

import { getSession } from "@/lib/auth/session";

import { SignupForm } from "./signup-form";

export const metadata: Metadata = { title: "Crear cuenta" };

// Depende de la cookie de sesión, no se puede prerrenderizar.
export const dynamic = "force-dynamic";

function safeNext(value: string | string[] | undefined): string {
  // Solo rutas internas: nada de `//host` ni `https://…`.
  return typeof value === "string" &&
    value.startsWith("/") &&
    !value.startsWith("//")
    ? value
    : "/";
}

export default async function SignupPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const next = safeNext(params.next);

  const session = await getSession();
  if (session) redirect(next);

  return <SignupForm next={next} />;
}
