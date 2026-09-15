import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";

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
 * —una palabra, no un cero en 3xl—. Cuando no lo está, cada métrica decide
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
      <Card>
        <CardHeader>
          <CardTitle className="text-sm font-medium">{title}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-1">
          <span className="text-sm font-medium">Vacía</span>
          <span className="text-muted-foreground text-xs">{emptyReason}</span>
        </CardContent>
      </Card>
    );
  }

  const metrics = [
    { value: pending, caption: "esperando" },
    { value: processing, caption: "en curso" },
    { value: dead, caption: "sin salida", isCrit: dead > 0 },
  ];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm font-medium">{title}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <div className="grid grid-cols-3 gap-2">
          {metrics.map((metric) => (
            <div
              key={metric.caption}
              className="flex flex-col justify-end gap-0.5"
            >
              {metric.value === 0 ? (
                <span className="text-muted-foreground text-sm">0</span>
              ) : (
                <span
                  className={cn(
                    "font-mono text-3xl font-semibold tabular-nums",
                    metric.isCrit && "text-destructive",
                  )}
                >
                  {metric.value}
                </span>
              )}
              <span className="text-muted-foreground text-xs">
                {metric.caption}
              </span>
            </div>
          ))}
        </div>
        {data.oldestPendingAt && (
          <span className="text-muted-foreground font-mono text-xs">
            la más vieja lleva{" "}
            {formatDuration(now - new Date(data.oldestPendingAt).getTime())}{" "}
            esperando
          </span>
        )}
      </CardContent>
    </Card>
  );
}
