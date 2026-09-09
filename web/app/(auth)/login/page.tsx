import type { Metadata } from "next";
import { redirect } from "next/navigation";

import { enabledSocialProviders } from "@/lib/auth/providers";
import { getSession } from "@/lib/auth/session";

import { LoginForm } from "./login-form";

export const metadata: Metadata = { title: "Iniciar sesión" };

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

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const next = safeNext(params.next);

  const session = await getSession();
  if (session) redirect(next);

  return <LoginForm providers={enabledSocialProviders()} next={next} />;
}
