import { LogOut } from "lucide-react";

/**
 * Franja persistente mientras dura una impersonación (Fase 5) — patrón
 * estándar (GitHub, Intercom, WHMCS): visible en cada pantalla, con quién se
 * está viendo y una salida siempre a mano, sin importar qué tan
 * inofensivo parezca el resto de la pantalla.
 *
 * "Salir" es un `<a>` **crudo**, no `next/link`: `/api/impersonate/end` es
 * un route handler, no una página, y el router de la app intenta una
 * transición del lado del cliente en vez de dejar que el navegador la
 * navegue de verdad — el resultado observado es un enlace que no hace
 * nada más que un "atrás" del historial. Una navegación completa (recarga
 * dura) es justo lo que hace falta acá: la cookie se borra en el servidor y
 * el layout siguiente ya no la ve.
 */
export function ImpersonationBanner({ email }: { email: string }) {
  return (
    <div className="flex shrink-0 items-center justify-center gap-3 border-b border-amber-500/30 bg-amber-500/10 px-4 py-1.5 text-sm text-amber-800 dark:text-amber-300">
      <span>
        Estás viendo como <strong className="font-medium">{email}</strong> —
        solo lectura.
      </span>
      <a
        href="/api/impersonate/end"
        className="inline-flex items-center gap-1 font-medium underline underline-offset-2 hover:no-underline"
      >
        <LogOut className="size-3.5" aria-hidden />
        Salir
      </a>
    </div>
  );
}
