"use client";

import { ShieldAlert } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Skeleton } from "@/components/ui/skeleton";
import { ApiError } from "@/lib/api/problem";

import { heroFor, QUEUES, workerFraction } from "./hero";
import "./ops-console.css";
import { QueueCard } from "./queue-card";
import { SequenceRiskList } from "./sequence-risk-list";
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
    <div className="ops-console">
      <section className="hero flex items-start gap-3">
        <div className="pt-2">
          <StatusDot severity={hero.severity} />
        </div>
        <div>
          <h1 className="hero__headline">{hero.headline}</h1>
          <p className="hero__support">{hero.support}</p>
        </div>
      </section>

      <div className="rule" />

      <section className="section">
        <div className="section__label-row">
          <h2 className="section__label">Workers</h2>
          <span className="section__hint">
            ordenado por presupuesto consumido
          </span>
        </div>
        <div className="worker-list">
          {sortedWorkers.map((worker) => (
            <WorkerRow key={worker.name} worker={worker} now={now} />
          ))}
        </div>
      </section>

      <div className="rule" />

      <section className="section">
        <h2 className="section__label">Colas</h2>
        <div className="queue-grid">
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
      </section>

      <div className="rule" />

      <section className="section">
        <div className="section__label-row">
          <h2 className="section__label">Secuencias por agotarse</h2>
          {data.sequencesAtRisk.length > 0 && (
            <span className="section__hint">
              ordenado por menor stock restante
            </span>
          )}
        </div>
        <SequenceRiskList tenants={data.sequencesAtRisk} />
      </section>
    </div>
  );
}

function LoadingState() {
  return (
    <div className="ops-console">
      <div className="flex items-start gap-3">
        <Skeleton className="mt-2 h-2 w-2 rounded-full" />
        <div className="flex flex-col gap-2">
          <Skeleton className="h-7 w-56" />
          <Skeleton className="h-4 w-96" />
        </div>
      </div>

      <div className="rule" />

      <div className="flex flex-col gap-2">
        <Skeleton className="h-4 w-24" />
        {Array.from({ length: 5 }).map((_, index) => (
          <div key={index} className="flex items-center justify-between py-2">
            <Skeleton className="h-4 w-40" />
            <Skeleton className="h-4 w-48" />
          </div>
        ))}
      </div>

      <div className="rule" />

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {Array.from({ length: 2 }).map((_, index) => (
          <Skeleton key={index} className="h-28 rounded-lg" />
        ))}
      </div>
    </div>
  );
}
