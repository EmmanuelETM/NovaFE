"use client";

import { ShieldAlert } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import {
  Card,
  CardAction,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { ApiError } from "@/lib/api/problem";

import { heroFor, QUEUES, workerFraction } from "./hero";
import { QueueCard } from "./queue-card";
import { SequencesCard } from "./sequences-card";
import { StatusDot } from "./status-dot";
import { useNow } from "./use-now";
import { useOpsStatus } from "./use-ops-status";
import { WorkerRow } from "./worker-row";

export function OpsStatusScreen() {
  const { data, isPending, error } = useOpsStatus();
  const now = useNow();

  if (isPending) return <LoadingState />;

  if (error) {
    if (error instanceof ApiError && error.isAccessDenied) {
      return (
        <Alert>
          <ShieldAlert />
          <AlertTitle>Necesitas rol de operador</AlertTitle>
          <AlertDescription>
            Esta pantalla es para operadores del SaaS. Tu cuenta no tiene ese
            acceso.
          </AlertDescription>
        </Alert>
      );
    }

    return (
      <Alert variant="destructive">
        <ShieldAlert />
        <AlertTitle>No se pudo cargar el estado</AlertTitle>
        <AlertDescription>
          {error instanceof Error ? error.message : "Error desconocido."}
        </AlertDescription>
      </Alert>
    );
  }

  const hero = heroFor(data, now);
  const sortedWorkers = [...data.workers].sort(
    (a, b) => workerFraction(b, now) - workerFraction(a, now),
  );

  return (
    <div className="flex flex-col gap-6">
      {data.contingencyActive && (
        <Alert variant="destructive">
          <ShieldAlert />
          <AlertTitle>Contingencia activa (M11 Tipo 1)</AlertTitle>
          <AlertDescription>
            El envío a la DGII lleva estancado más de lo normal.
            platform.contingency_mode se prendió solo — los comprobantes siguen
            firmándose y reintentando, y la Representación Impresa de los nuevos
            ya lleva la leyenda de contingencia. Se apaga solo cuando el envío
            se pone al día.
          </AlertDescription>
        </Alert>
      )}

      <div className="flex items-start gap-3">
        <div className="pt-1.5">
          <StatusDot severity={hero.severity} />
        </div>
        <div className="flex flex-col gap-1">
          <h1 className="text-xl font-semibold tracking-tight text-balance">
            {hero.headline}
          </h1>
          <p className="text-muted-foreground text-sm">{hero.support}</p>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-sm font-medium">Workers</CardTitle>
          <CardAction>
            <span className="text-muted-foreground text-xs">
              ordenado por presupuesto consumido
            </span>
          </CardAction>
        </CardHeader>
        <CardContent className="divide-border divide-y py-0">
          {sortedWorkers.map((worker) => (
            <WorkerRow key={worker.name} worker={worker} now={now} />
          ))}
        </CardContent>
      </Card>

      <div className="grid gap-4 sm:grid-cols-2">
        {QUEUES.map((queue) => (
          <QueueCard
            key={queue.key}
            title={queue.title}
            data={data[queue.field]}
            now={now}
            emptyReason={queue.emptyReason}
          />
        ))}
      </div>

      <SequencesCard tenants={data.sequencesAtRisk} />
    </div>
  );
}

function LoadingState() {
  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-start gap-3">
        <Skeleton className="mt-1.5 h-2.5 w-2.5 rounded-full" />
        <div className="flex flex-col gap-2">
          <Skeleton className="h-6 w-56" />
          <Skeleton className="h-4 w-96" />
        </div>
      </div>

      <Card>
        <CardHeader>
          <Skeleton className="h-4 w-16" />
        </CardHeader>
        <CardContent className="flex flex-col gap-4 py-0 pb-5">
          {Array.from({ length: 5 }).map((_, index) => (
            <div key={index} className="flex justify-between gap-6 py-1">
              <Skeleton className="h-4 w-40" />
              <Skeleton className="h-4 w-48" />
            </div>
          ))}
        </CardContent>
      </Card>

      <div className="grid gap-4 sm:grid-cols-2">
        {Array.from({ length: 2 }).map((_, index) => (
          <Skeleton key={index} className="h-28 rounded-2xl" />
        ))}
      </div>

      <Skeleton className="h-28 rounded-2xl" />
    </div>
  );
}
