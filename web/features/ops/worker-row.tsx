import { clamp, formatBudgetPair, parseTimeSpan } from "./format";
import { WORKER_LABELS, workerFraction, workerSeverity } from "./hero";
import type { WorkerStatus } from "./types";

interface WorkerRowProps {
  worker: WorkerStatus;
  now: number;
}

/**
 * Barra de presupuesto de silencio: el elemento con permiso de destacar de
 * toda la consola. `pct` y el par `actual / presupuesto` dicen lo mismo en
 * dos formatos — uno compacto para escanear, el otro con el denominador
 * explícito para quien está ajustando `maxSilence`.
 */
export function WorkerRow({ worker, now }: WorkerRowProps) {
  const fraction = workerFraction(worker, now);
  const severity = workerSeverity(worker, now);
  const pct = Math.round(fraction * 100);
  const lastBeatMs = new Date(worker.lastBeatAt).getTime();

  return (
    <div className="worker-row">
      <div className="worker-row__identity">
        <span className="worker-row__label">
          {WORKER_LABELS[worker.name] ?? worker.name}
        </span>
        <span className="worker-row__slug">{worker.name}</span>
      </div>

      <div className="worker-row__meta">
        <span className={`worker-row__pct worker-row__pct--${severity}`}>
          {pct}%
        </span>
        <div
          className="budget-bar"
          role="img"
          aria-label={`${pct}% del presupuesto de silencio consumido`}
        >
          <div
            className={`budget-bar__fill budget-bar__fill--${severity}`}
            style={{ width: `${clamp(fraction, 0, 1) * 100}%` }}
          />
        </div>
        <span className="worker-row__pair">
          {formatBudgetPair(now - lastBeatMs, parseTimeSpan(worker.maxSilence))}
        </span>
      </div>
    </div>
  );
}
