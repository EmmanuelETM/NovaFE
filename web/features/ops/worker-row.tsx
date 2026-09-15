import { Progress as ProgressPrimitive } from "@base-ui/react/progress";

import { ProgressIndicator, ProgressTrack } from "@/components/ui/progress";
import { cn } from "@/lib/utils";

import { clamp, formatBudgetPair, parseTimeSpan } from "./format";
import { WORKER_LABELS, workerFraction, workerSeverity } from "./hero";
import type { WorkerStatus } from "./types";

interface WorkerRowProps {
  worker: WorkerStatus;
  now: number;
}

/**
 * Barra de presupuesto de silencio: el elemento con permiso de destacar de
 * toda la pantalla. Nombre arriba, barra abajo — así la barra se lleva todo
 * el ancho de la fila en vez de competir por espacio con la identidad. `pct`
 * y el par `actual / presupuesto` dicen lo mismo en dos formatos — uno
 * compacto para escanear, el otro con el denominador explícito para quien
 * está ajustando `maxSilence`.
 */
export function WorkerRow({ worker, now }: WorkerRowProps) {
  const fraction = workerFraction(worker, now);
  const severity = workerSeverity(worker, now);
  const pct = Math.round(fraction * 100);
  const lastBeatMs = new Date(worker.lastBeatAt).getTime();

  return (
    <div className="flex flex-col gap-2 py-3">
      <div className="flex min-w-0 items-baseline gap-2.5">
        <span className="truncate text-sm font-medium">
          {WORKER_LABELS[worker.name] ?? worker.name}
        </span>
        <span className="text-muted-foreground shrink-0 font-mono text-xs">
          {worker.name}
        </span>
      </div>

      <div className="flex items-center gap-2.5">
        <span
          className={cn(
            "shrink-0 font-mono text-sm font-semibold tabular-nums",
            severity === "ok" && "text-muted-foreground",
            severity === "warn" && "text-amber-600 dark:text-amber-500",
            severity === "crit" && "text-destructive",
          )}
        >
          {pct}%
        </span>

        <ProgressPrimitive.Root
          value={clamp(pct, 0, 100)}
          className="flex-1"
          aria-label={`${pct}% del presupuesto de silencio consumido`}
        >
          <ProgressTrack>
            <ProgressIndicator
              className={cn(
                severity === "ok" && "bg-muted-foreground/40",
                severity === "warn" && "bg-amber-500",
                severity === "crit" && "bg-destructive",
              )}
            />
          </ProgressTrack>
        </ProgressPrimitive.Root>

        <span className="text-muted-foreground shrink-0 font-mono text-xs tabular-nums">
          {formatBudgetPair(now - lastBeatMs, parseTimeSpan(worker.maxSilence))}
        </span>
      </div>
    </div>
  );
}
