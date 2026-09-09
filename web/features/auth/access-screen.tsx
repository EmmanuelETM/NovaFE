"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { LogIn, RefreshCw, ShieldAlert } from "lucide-react";

import { Button } from "@/components/ui/button";

interface AccessScreenProps {
  /** El estado que devolvió la API, o 0 si no se pudo llegar a ella. */
  status: number;
  /** El `title` del ProblemDetails. Ya viene listo para mostrar. */
  message: string;
}

/**
 * Lo que se ve cuando no se pudo establecer quién eres.
 *
 * El layout necesita el perfil (`GET /users/me`) para pintar la navegación; si
 * falla, no hay app que mostrar. Tres casos, que piden cosas distintas:
 *
 * - **401** — no hay sesión (o venció). Enlace a iniciar sesión.
 * - **403** — autenticado pero sin alta en `platform_users`. Que un admin te dé de alta.
 * - **otro / 0** — la API no respondió. Reintentar.
 */
export function AccessScreen({ status, message }: AccessScreenProps) {
  const router = useRouter();

  const sinSesion = status === 401;
  const sinAlta = status === 403;

  return (
    <div className="flex min-h-svh flex-col items-center justify-center gap-6 p-6 text-center">
      <div className="flex max-w-md flex-col gap-2">
        <div className="text-muted-foreground flex items-center justify-center gap-2 text-sm">
          <ShieldAlert className="size-4" aria-hidden />
          {sinSesion
            ? "Sesión no iniciada"
            : sinAlta
              ? "Sin acceso"
              : "No se pudo verificar tu acceso"}
        </div>

        <h1 className="text-xl font-semibold tracking-tight">{message}</h1>

        <p className="text-muted-foreground text-sm">
          {sinSesion
            ? "Iniciá sesión para entrar al panel."
            : sinAlta
              ? "Tu cuenta todavía no está dada de alta. Pedile a un administrador que te registre con este correo."
              : "El servidor no respondió. Si vuelve a pasar, avisale al equipo."}
        </p>
      </div>

      {sinSesion ? (
        <Button render={<Link href="/login" />}>
          <LogIn aria-hidden />
          Iniciar sesión
        </Button>
      ) : sinAlta ? null : (
        <Button variant="outline" onClick={() => router.refresh()}>
          <RefreshCw aria-hidden />
          Reintentar
        </Button>
      )}
    </div>
  );
}
