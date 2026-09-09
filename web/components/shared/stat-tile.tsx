import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

interface StatTileProps {
  /** Qué es la cifra. Va arriba y en pequeño: se lee después del número, no antes. */
  label: string;
  /** La cifra, **ya formateada**. La pantalla no calcula dinero. */
  value: string;
  /** El contexto que hace que la cifra signifique algo: «12 ventas», «sin enviar». */
  hint?: string;
  /**
   * Que la cifra pida atención. Es la única desviación del acento único que el proyecto
   * admite, y solo para estado — nunca para destacar algo por ser importante.
   */
  highlight?: boolean;
  /** Esqueleto con la forma del valor, para que la fila no salte cuando llegan los datos. */
  loading?: boolean;
}

/**
 * Una cifra con su etiqueta: el ladrillo de cualquier fila de resumen.
 *
 * Vive en `shared/` y no dentro de una feature porque lo usan el panel de inicio y el reporte
 * de créditos. Cuando nació era local del reporte, y dejarlo ahí habría dado dos tiles
 * parecidos que se separan a la primera de cambio — que es como una interfaz deja de verse
 * hecha por la misma mano.
 *
 * El contenedor es `bg-card rounded-2xl border`, el mismo de las conciliaciones. No usa
 * `Card` a propósito: el resto del proyecto tampoco, y mezclar los dos daría dos radios y dos
 * paddings distintos para la misma idea.
 */
export function StatTile({
  label,
  value,
  hint,
  highlight = false,
  loading = false,
}: StatTileProps) {
  return (
    <div className="bg-card flex flex-col gap-1 rounded-2xl border p-4">
      <span className="text-muted-foreground text-xs">{label}</span>

      {loading ? (
        <Skeleton className="h-8 w-28" />
      ) : (
        <span
          className={cn(
            "text-2xl font-semibold tabular-nums",
            highlight && "text-amber-700 dark:text-amber-400",
          )}
        >
          {value}
        </span>
      )}

      {/* La pista se calla mientras carga. Casi siempre depende de los datos —«RD$0.00 por
          venta»— y mostrarla con el valor todavía en esqueleto afirma algo que no se sabe. */}
      {hint && !loading ? (
        <span className="text-muted-foreground text-xs">{hint}</span>
      ) : null}
    </div>
  );
}
