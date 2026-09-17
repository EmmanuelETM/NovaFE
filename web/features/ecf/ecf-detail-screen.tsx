"use client";

import type { ReactNode } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useCan } from "@/features/auth/use-current-user";
import { ROLE } from "@/features/auth/roles";
import { formatCalendarDate, formatDateTime } from "@/lib/format";

import {
  ecfStatusLabel,
  ecfStatusVariant,
  ecfTypeLabel,
  isRetriable,
} from "./types";
import { useEcf, useRetryEcf } from "./use-ecf";

/** Un dato fiscal en la grilla: etiqueta + valor, con el valor en fuente monoespaciada si aplica. */
function Field({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-muted-foreground text-xs">{label}</span>
      <span className="text-sm">{value}</span>
    </div>
  );
}

export function EcfDetailScreen({ ecfId }: { ecfId: string }) {
  const { data: ecf, isPending, error } = useEcf(ecfId);
  const canRetry = useCan(ROLE.emisor);
  const retry = useRetryEcf();

  if (isPending) {
    return <p className="text-muted-foreground text-sm">Cargando…</p>;
  }

  if (error || !ecf) {
    return (
      <p className="text-destructive text-sm">
        No se pudo cargar este comprobante.
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="font-mono text-xl font-semibold">{ecf.encf}</h2>
        <Badge variant={ecfStatusVariant(ecf.status)}>
          {ecfStatusLabel(ecf.status)}
        </Badge>
        {ecf.signedDuringContingency && (
          <Badge variant="outline">Firmado en contingencia</Badge>
        )}
      </div>

      {ecf.toleranceWarning && (
        <p className="bg-muted rounded-lg p-3 text-sm">
          {ecf.toleranceWarning}
        </p>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Datos fiscales</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <Field label="Tipo" value={ecfTypeLabel(ecf.type)} />
          <Field label="Ambiente" value={ecf.environment} />
          <Field
            label="Fecha de emisión"
            value={formatCalendarDate(ecf.issueDate)}
          />
          <Field label="Firmado" value={formatDateTime(ecf.signedAt)} />
          <Field
            label="Vencimiento de secuencia"
            value={
              ecf.sequenceExpiresOn
                ? formatCalendarDate(ecf.sequenceExpiresOn)
                : "No vence"
            }
          />
          <Field label="Código de seguridad" value={ecf.securityCode} />
          {ecf.internalNumber && (
            <Field label="Número interno" value={ecf.internalNumber} />
          )}
        </CardContent>
      </Card>

      {ecf.dgii && (
        <Card>
          <CardHeader>
            <CardTitle>Intercambio con la DGII</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <Field label="TrackId" value={ecf.dgii.trackId ?? "—"} />
            <Field label="Estado DGII" value={ecf.dgii.status ?? "—"} />
            <Field
              label="Enviado"
              value={
                ecf.dgii.submittedAt
                  ? formatDateTime(ecf.dgii.submittedAt)
                  : "—"
              }
            />
            <Field
              label="Resuelto"
              value={
                ecf.dgii.processedAt
                  ? formatDateTime(ecf.dgii.processedAt)
                  : "—"
              }
            />
            {ecf.dgii.messages.length > 0 && (
              <div className="col-span-full flex flex-col gap-1">
                <span className="text-muted-foreground text-xs">
                  Mensajes de la DGII
                </span>
                <ul className="list-disc pl-5 text-sm">
                  {ecf.dgii.messages.map((message, index) => (
                    <li key={index}>
                      {[message.code, message.value]
                        .filter(Boolean)
                        .join(": ")}
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      <div className="flex flex-wrap gap-2">
        {/*
          El proxy (`app/api/backend/[...path]`) antepone `/api/{version}` solo,
          así que se arma la ruta a partir del id en vez de usar `ecf.links`:
          esos vienen con el `/api/v1` completo, pensados para un consumidor
          directo de la API (una API key), no para el proxy del navegador.
        */}
        <Button
          variant="outline"
          onClick={() =>
            window.open(`/api/backend/ecf/${ecf.id}/xml`, "_blank")
          }
        >
          Ver XML
        </Button>
        <Button
          variant="outline"
          onClick={() =>
            window.open(`/api/backend/ecf/${ecf.id}/representation`, "_blank")
          }
        >
          Ver representación impresa
        </Button>
        {canRetry && isRetriable(ecf.status) && (
          <Button
            disabled={retry.isPending}
            onClick={() => retry.mutate(ecf.id)}
          >
            Reintentar envío
          </Button>
        )}
      </div>
    </div>
  );
}
