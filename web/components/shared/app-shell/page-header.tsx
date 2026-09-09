import type { ReactNode } from "react";

import { findNavItem } from "@/lib/navigation";

interface PageHeaderProps {
  /** La ruta de la pantalla. El título y la descripción salen de `lib/navigation`. */
  href: string;
  /** Acciones a nivel de pantalla, alineadas a la derecha. */
  actions?: ReactNode;
}

/**
 * El encabezado de una pantalla.
 *
 * El texto **no se escribe aquí**: sale de `lib/navigation`, la misma definición que pinta
 * el sidebar. Es lo que garantiza que el enlace que se pulsó y el título que aparece digan
 * lo mismo — cuando se escriben en dos archivos, tarde o temprano dejan de coincidir y el
 * usuario duda de si llegó a donde quería.
 */
export function PageHeader({ href, actions }: PageHeaderProps) {
  const item = findNavItem(href);

  if (!item) {
    throw new Error(
      `PageHeader: la ruta ${href} no está en lib/navigation. Agrégala ahí, que es de donde salen el sidebar y el título.`,
    );
  }

  return (
    <header className="flex flex-wrap items-start justify-between gap-3">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold tracking-tight">{item.label}</h1>
        <p className="text-muted-foreground text-sm">{item.description}</p>
      </div>

      {actions}
    </header>
  );
}
