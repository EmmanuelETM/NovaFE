import Link from "next/link";
import { ShieldAlert } from "lucide-react";

import { buttonVariants } from "@/components/ui/button";
import { roleLabel } from "./roles";

/**
 * Se ve cuando `GET /users/me` respondió bien, pero el rol no alcanza para
 * esta sección — a diferencia de `AccessScreen`, acá sí sabemos quién sos,
 * simplemente no es tu lugar. Hace falta esta pantalla además del filtro de
 * `lib/navigation.ts` porque ese filtro solo esconde el enlace del sidebar;
 * quien escribe la URL a mano (o la tenía guardada) igual llega al layout, y
 * "esconder no es proteger" — la API ya lo protege del lado de datos, pero
 * sin esto el layout entero se pintaba igual para cualquiera.
 */
export function InsufficientRoleScreen({ role }: { role: string }) {
  return (
    <div className="flex min-h-svh flex-col items-center justify-center gap-6 p-6 text-center">
      <div className="flex max-w-md flex-col gap-2">
        <div className="text-muted-foreground flex items-center justify-center gap-2 text-sm">
          <ShieldAlert className="size-4" aria-hidden />
          Sin acceso
        </div>

        <h1 className="text-xl font-semibold tracking-tight">
          Esta sección es solo para operadores de NovaFE.
        </h1>

        <p className="text-muted-foreground text-sm">
          Tu cuenta tiene el rol «{roleLabel(role)}», que no alcanza para entrar
          acá.
        </p>
      </div>

      <Link href="/" className={buttonVariants({ variant: "outline" })}>
        Volver al inicio
      </Link>
    </div>
  );
}
