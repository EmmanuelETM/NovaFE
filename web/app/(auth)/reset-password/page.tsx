import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";

import { getSession } from "@/lib/auth/session";

import { AuthCard } from "../auth-card";
import { ResetPasswordForm } from "./reset-password-form";

export const metadata: Metadata = { title: "Restablecer contraseña" };

// Depende de la cookie de sesión, no se puede prerrenderizar.
export const dynamic = "force-dynamic";

export default async function ResetPasswordPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const session = await getSession();
  if (session) redirect("/");

  const { token } = await searchParams;

  if (typeof token !== "string" || token.length === 0) {
    return (
      <AuthCard
        title="Enlace inválido"
        description="Este enlace de restablecimiento ya no sirve."
      >
        <p className="text-center text-sm">
          Puede que haya expirado o que ya se haya usado.
        </p>
        <Link
          href="/forgot-password"
          className="text-foreground text-center text-sm underline underline-offset-4"
        >
          Pedir un enlace nuevo
        </Link>
      </AuthCard>
    );
  }

  return <ResetPasswordForm token={token} />;
}
