import {
  Card,
  CardAction,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { cn } from "@/lib/utils";

import { SEQUENCE_CRIT_REMAINING } from "./hero";
import type { SequenceAtRisk } from "./types";

interface SequencesCardProps {
  tenants: SequenceAtRisk[];
}

/**
 * Inventario, no trabajo pendiente — por eso no comparte tarjeta con las
 * colas. En un sistema multitenant un agregado ("4 activas, 1 baja") no le
 * dice a nadie qué hacer: no identifica al tenant. La lista nombra
 * exactamente a quién avisarle. Ya viene ordenada por menor stock restante
 * (misma definición de "bajo stock" que usa el propio tenant).
 */
export function SequencesCard({ tenants }: SequencesCardProps) {
  const isEmpty = tenants.length === 0;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm font-medium">
          Secuencias por agotarse
        </CardTitle>
        {!isEmpty && (
          <CardAction>
            <span className="text-muted-foreground text-xs">
              ordenado por menor stock restante
            </span>
          </CardAction>
        )}
      </CardHeader>
      <CardContent className={cn(!isEmpty && "divide-border divide-y py-0")}>
        {isEmpty ? (
          <p className="text-muted-foreground text-sm">
            Ningún tenant tiene secuencias por agotarse esta semana.
          </p>
        ) : (
          tenants.map((item) => {
            const remaining = Number(item.remaining);
            const isCrit = remaining < SEQUENCE_CRIT_REMAINING;
            return (
              <div
                key={`${item.tenantName}-${item.type}`}
                className="flex flex-wrap items-center justify-between gap-x-4 gap-y-1 py-3"
              >
                <span className="text-sm font-medium">{item.tenantName}</span>
                <div className="flex items-center gap-3">
                  <span className="text-muted-foreground font-mono text-xs">
                    {item.type}
                  </span>
                  <span
                    className={cn(
                      "w-28 shrink-0 text-right font-mono text-xs font-semibold tabular-nums",
                      isCrit
                        ? "text-destructive"
                        : "text-amber-600 dark:text-amber-500",
                    )}
                  >
                    {remaining.toLocaleString("es-DO")} restantes
                  </span>
                </div>
              </div>
            );
          })
        )}
      </CardContent>
    </Card>
  );
}
