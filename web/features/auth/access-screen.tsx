"use client";

import { useRouter } from "next/navigation";
import { RefreshCw, ShieldAlert } from "lucide-react";

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
 * El esqueleto de la aplicación necesita el perfil para saber qué enlaces mostrar, así que
 * si `GET /users/me` falla no hay aplicación que pintar. Y eso es información, no un
 * estorbo: en producción este es exactamente el camino de alguien que no está dado de alta,
 * y decírselo con su nombre propio ahorra la llamada a soporte que empieza con «no me deja
 * entrar».
 *
 * Se distinguen los dos casos porque piden cosas distintas: un 403 no se arregla
 * reintentando, y una API caída no se arregla hablando con nadie.
 */
export function AccessScreen({ status, message }: AccessScreenProps) {
  const router = useRouter();

  const sinPermiso = status === 401 || status === 403;

  return (
    <div className="flex min-h-svh flex-col items-center justify-center gap-6 p-6 text-center">
      <div className="flex max-w-md flex-col gap-2">
        <div className="text-muted-foreground flex items-center justify-center gap-2 text-sm">
          <ShieldAlert className="size-4" aria-hidden />
          {sinPermiso ? "Sin acceso" : "No se pudo verificar tu acceso"}
        </div>

        <h1 className="text-xl font-semibold tracking-tight">{message}</h1>

        <p className="text-muted-foreground text-sm">
          {sinPermiso
            ? "Tu cuenta tiene que estar dada de alta antes de poder entrar. Pídele a un administrador que te registre."
            : "El servidor no respondió. Si vuelve a pasar, avísale al equipo de soporte."}
        </p>
      </div>

      {!sinPermiso && (
        <Button variant="outline" onClick={() => router.refresh()}>
          <RefreshCw aria-hidden />
          Reintentar
        </Button>
      )}
    </div>
  );
}
