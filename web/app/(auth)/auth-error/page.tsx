import type { Metadata } from "next";
import Link from "next/link";

import { Button } from "@/components/ui/button";

export const metadata: Metadata = { title: "No se pudo iniciar sesión" };

/**
 * Aterrizaje del `errorCallbackURL` de Better Auth cuando el OAuth falla
 * (el usuario canceló, el provider rechazó, credenciales mal configuradas…).
 */
export default async function AuthErrorPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { error } = await searchParams;
  const detail = typeof error === "string" ? error : null;

  return (
    <div className="flex max-w-md flex-col items-center gap-4 text-center">
      <h1 className="text-xl font-semibold tracking-tight">
        No se pudo iniciar sesión
      </h1>
      <p className="text-muted-foreground text-sm">
        {detail === "access_denied"
          ? "Cancelaste el acceso desde el proveedor."
          : "El proveedor de identidad rechazó la solicitud. Probá de nuevo; si sigue fallando, avisá al equipo."}
      </p>
      <Button render={<Link href="/login" />}>Volver a intentar</Button>
    </div>
  );
}
