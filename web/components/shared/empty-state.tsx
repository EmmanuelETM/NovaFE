import type { LucideIcon } from "lucide-react";
import { Inbox } from "lucide-react";

import { cn } from "@/lib/utils";

interface EmptyStateProps {
  title: string;
  description?: string;
  /** La acción que resuelve el vacío: «Crear el primer webhook». */
  action?: React.ReactNode;
  icon?: LucideIcon;
  className?: string;
}

/**
 * El mismo lenguaje visual que `DataTableEmpty`
 * (`components/shared/data-table/data-table-states.tsx`), pero para fuera de
 * una tabla: una tarjeta o una sección completa vacía (el grid de tenants de
 * una organización, un listado sin resultados). No se reutiliza el otro
 * directamente porque ese vive envuelto en `<TableRow>`/`<TableCell>`.
 */
export function EmptyState({
  title,
  description,
  action,
  icon: Icon = Inbox,
  className,
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        "border-border/60 flex flex-col items-center justify-center gap-2 rounded-2xl border border-dashed p-10 text-center",
        className,
      )}
    >
      <Icon className="text-muted-foreground/50 size-8" aria-hidden />
      <p className="font-medium">{title}</p>
      {description && (
        <p className="text-muted-foreground max-w-sm text-sm">{description}</p>
      )}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}
