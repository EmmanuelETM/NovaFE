import { formatDuration } from "./format";
import type { OutboxStatus } from "./types";

interface QueueCardProps {
  title: string;
  data: OutboxStatus;
  now: number;
  emptyReason: string;
}

/**
 * Trabajo pendiente que debería llegar a cero. Silenciosa cuando lo está
 * —una palabra, no un cero en 32px—. Cuando no lo está, cada métrica decide
 * por su cuenta si merece una cifra grande: un cero nunca la merece, aunque
 * sus vecinas en la misma tarjeta sí tengan algo que mostrar.
 */
export function QueueCard({ title, data, now, emptyReason }: QueueCardProps) {
  const pending = Number(data.pending);
  const processing = Number(data.processing);
  const dead = Number(data.dead);
  const isEmpty = pending === 0 && processing === 0 && dead === 0;

  if (isEmpty) {
    return (
      <div className="queue-card queue-card--empty">
        <span className="queue-card__title">{title}</span>
        <div className="queue-card__empty-body">
          <span className="queue-card__empty-word">Vacía</span>
          <span className="queue-card__empty-reason">{emptyReason}</span>
        </div>
      </div>
    );
  }

  const metrics = [
    { value: pending, caption: "esperando" },
    { value: processing, caption: "en curso" },
    { value: dead, caption: "sin salida", isCrit: dead > 0 },
  ];

  return (
    <div className="queue-card">
      <span className="queue-card__title">{title}</span>
      <div className="queue-card__numbers">
        {metrics.map((metric) => (
          <div className="queue-card__number-block" key={metric.caption}>
            {metric.value === 0 ? (
              <span className="queue-card__number queue-card__number--zero">
                0
              </span>
            ) : (
              <span
                className={`queue-card__number ${metric.isCrit ? "is-crit" : ""}`}
              >
                {metric.value}
              </span>
            )}
            <span className="queue-card__caption">{metric.caption}</span>
          </div>
        ))}
      </div>
      {data.oldestPendingAt && (
        <span className="queue-card__oldest">
          la más vieja lleva{" "}
          {formatDuration(now - new Date(data.oldestPendingAt).getTime())}{" "}
          esperando
        </span>
      )}
    </div>
  );
}
