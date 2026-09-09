"use client";

import { useState } from "react";
import { usePathname } from "next/navigation";

import { SidebarProvider } from "@/components/ui/sidebar";

/**
 * Las pantallas que arrancan con el sidebar colapsado porque el ancho es el recurso
 * escaso — un punto de venta, un editor a pantalla completa. Vacío por defecto.
 */
const COLAPSADAS = new Set<string>();

interface AppSidebarProviderProps {
  /** La preferencia guardada en la cookie, resuelta en el servidor. */
  defaultOpen: boolean;
  children: React.ReactNode;
}

/**
 * El estado del sidebar, con excepciones por ruta.
 *
 * Una pantalla listada en `COLAPSADAS` **arranca colapsada**, sin importar cómo esté en el
 * resto de la aplicación. Colapsado no es escondido —el sidebar es `collapsible="icon"`—
 * así que la navegación sigue ahí, en su franja de iconos.
 *
 * <b>Se decide aquí y no en el layout</b> porque un layout de servidor no sabe la ruta.
 * `usePathname` sí, y también durante el render del servidor, así que el primer HTML ya
 * llega colapsado y no hay parpadeo.
 *
 * <b>Abrirlo dentro de una de esas pantallas funciona, y dura lo que dure la visita.</b>
 * Es un override temporal y no un cambio de preferencia: al salir se olvida. Si en lugar
 * de esto se guardara, un vistazo a la navegación en medio de una tarea cambiaría cómo
 * arranca la pantalla mañana.
 */
export function AppSidebarProvider({
  defaultOpen,
  children,
}: AppSidebarProviderProps) {
  const pathname = usePathname();
  const esColapsada = COLAPSADAS.has(pathname);

  const [preferencia, setPreferencia] = useState(defaultOpen);

  // El override guarda **de qué ruta era**. Así caduca solo al navegar, sin un efecto que
  // lo limpie: un `setState` dentro de un efecto para deshacer lo que el render ya sabe es
  // justo lo que hay que evitar, y aquí es una comparación.
  const [temporal, setTemporal] = useState<{
    path: string;
    open: boolean;
  } | null>(null);

  const override = temporal?.path === pathname ? temporal.open : null;
  const open = esColapsada ? (override ?? false) : preferencia;

  return (
    <SidebarProvider
      open={open}
      onOpenChange={
        esColapsada
          ? (abierto) => setTemporal({ path: pathname, open: abierto })
          : setPreferencia
      }
      /* `h-svh` no es redundante con el `min-h-svh` que trae el componente generado: con
         solo un mínimo, el marco **crece** con su contenido, y entonces el `overflow-y-auto`
         del contenedor de páginas nunca recorta nada porque su alto también creció. El
         síntoma no se parece a la causa —el contenido empuja la barra de acciones fuera de
         la pantalla— y por eso el alto va fijo aquí, una sola vez, para toda la app. */
      className="h-svh overflow-hidden"
    >
      {children}
    </SidebarProvider>
  );
}
