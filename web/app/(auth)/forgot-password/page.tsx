import type { Metadata } from "next";
import { redirect } from "next/navigation";

import { getSession } from "@/lib/auth/session";

import { ForgotPasswordForm } from "./forgot-password-form";

export const metadata: Metadata = { title: "Olvidé mi contraseña" };

// Depende de la cookie de sesión, no se puede prerrenderizar.
export const dynamic = "force-dynamic";

export default async function ForgotPasswordPage() {
  const session = await getSession();
  if (session) redirect("/");

  return <ForgotPasswordForm />;
}
