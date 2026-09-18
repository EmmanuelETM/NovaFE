"use client";

import {
  Calendar,
  CalendarClock,
  CheckCircle2,
  FileCode,
  FileStack,
  FileText,
  Fingerprint,
  Hash,
  IdCard,
  Printer,
  Radar,
  RefreshCw,
  ShieldCheck,
  Tag,
  User,
  Wallet,
  type LucideIcon,
} from "lucide-react";
import type { ReactNode } from "react";
import QRCode from "react-qr-code";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { useCan } from "@/features/auth/use-current-user";
import { ROLE } from "@/features/auth/roles";
import { formatCalendarDate, formatDateTime, formatMoney } from "@/lib/format";

import {
  ecfEnvironmentBadgeClassName,
  ecfEnvironmentLabel,
  ecfStatusLabel,
  ecfStatusVariant,
  ecfTypeLabel,
  isRetriable,
} from "./types";
import { useEcf, useRetryEcf } from "./use-ecf";

/** Un dato fiscal en la grilla: icono + etiqueta + valor, con "N/A" si falta. */
function Field({
  icon: Icon,
  label,
  value,
  emptyLabel = "N/A",
  mono = false,
  className,
}: {
  icon: LucideIcon;
  label: string;
  value: ReactNode;
  emptyLabel?: string;
  mono?: boolean;
  className?: string;
}) {
  const isEmpty = value === null || value === undefined || value === "";

  return (
    <div className={`flex items-start gap-2 ${className ?? ""}`}>
      <Icon className="text-muted-foreground mt-0.5 size-4 shrink-0" />
      <div className="flex flex-col gap-0.5">
        <span className="text-muted-foreground text-xs">{label}</span>
        <span
          className={`text-sm ${mono ? "font-mono" : ""} ${
            isEmpty ? "text-muted-foreground" : ""
          }`}
          title={mono && !isEmpty ? String(value) : undefined}
        >
          {isEmpty ? emptyLabel : value}
        </span>
      </div>
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
        <h2 className="font-mono text-3xl font-bold">{ecf.encf}</h2>
        <Badge variant={ecfStatusVariant(ecf.status)}>
          {ecfStatusLabel(ecf.status)}
        </Badge>
        <Badge
          variant="outline"
          className={ecfEnvironmentBadgeClassName(ecf.environment)}
        >
          {ecfEnvironmentLabel(ecf.environment)}
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

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="flex flex-col gap-6 lg:col-span-2">
          <Card>
            <CardHeader>
              <CardTitle>Datos Fiscales</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-4 sm:grid-cols-2">
              <Field
                icon={FileText}
                label="Tipo"
                value={ecfTypeLabel(ecf.type)}
              />
              <Field
                icon={Calendar}
                label="Fecha de Emisión"
                value={formatCalendarDate(ecf.issueDate)}
              />
              <Field
                icon={CheckCircle2}
                label="Firmado"
                value={formatDateTime(ecf.signedAt)}
              />
              <Field
                icon={CalendarClock}
                label="Vencimiento de secuencia"
                value={
                  ecf.sequenceExpiresOn
                    ? formatCalendarDate(ecf.sequenceExpiresOn)
                    : "No vence"
                }
              />
              <Field
                icon={ShieldCheck}
                label="Código de Seguridad"
                value={ecf.securityCode}
                mono
              />
              {/* <Field
                icon={Hash}
                label="Hash del Documento"
                value={ecf.documentHash}
                mono
              /> */}
              <Field
                icon={Tag}
                label="Número interno"
                value={ecf.internalNumber}
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Datos del Cliente</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-4 sm:grid-cols-2">
              <Field
                icon={User}
                label="Nombre"
                value={ecf.buyerName}
                emptyLabel="No especificado"
              />
              <Field
                icon={IdCard}
                label="RNC/Cédula"
                value={ecf.buyerRnc}
                emptyLabel="No especificado"
              />
              <Field
                icon={Wallet}
                label="Monto Total"
                value={
                  <span className="text-lg font-semibold">
                    {formatMoney(Number(ecf.montoTotal))}
                  </span>
                }
                className="sm:col-span-2"
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Metadatos y DGII</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-4 sm:grid-cols-2">
              <Field
                icon={Fingerprint}
                label="Track ID"
                value={ecf.dgii?.trackId}
                mono
              />
              <Field
                icon={Radar}
                label="Estado en DGII"
                value={ecf.dgii?.status}
              />
              <Field
                icon={Calendar}
                label="Enviado"
                value={
                  ecf.dgii?.submittedAt
                    ? formatDateTime(ecf.dgii.submittedAt)
                    : null
                }
              />
              <Field
                icon={CheckCircle2}
                label="Resuelto"
                value={
                  ecf.dgii?.processedAt
                    ? formatDateTime(ecf.dgii.processedAt)
                    : null
                }
              />
              {ecf.dgii && ecf.dgii.messages.length > 0 && (
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
        </div>

        <div className="lg:col-span-1">
          <Card>
            <CardContent className="flex flex-col gap-4">
              <div className="mx-auto rounded-lg border p-4">
                <QRCode value={ecf.qrUrl} className="h-auto max-w-full" />
              </div>

              <Separator />

              {/*
                El proxy (`app/api/backend/[...path]`) antepone `/api/{version}`
                solo, así que se arma la ruta a partir del id en vez de usar
                `ecf.links`: esos vienen con el `/api/v1` completo, pensados
                para un consumidor directo de la API (una API key), no para el
                proxy del navegador.
              */}
              <div className="flex flex-col gap-2">
                <Button
                  variant="outline"
                  onClick={() =>
                    window.open(`/api/backend/ecf/${ecf.id}/xml`, "_blank")
                  }
                >
                  <FileCode /> Ver XML
                </Button>
                {ecf.submitsRfce && (
                  <Button
                    variant="outline"
                    onClick={() =>
                      window.open(
                        `/api/backend/ecf/${ecf.id}/xml?rfce=true`,
                        "_blank",
                      )
                    }
                  >
                    <FileStack /> Ver RFCE
                  </Button>
                )}
                <Button
                  variant="outline"
                  onClick={() =>
                    window.open(
                      `/api/backend/ecf/${ecf.id}/representation`,
                      "_blank",
                    )
                  }
                >
                  <Printer /> Ver RI
                </Button>
                {canRetry && isRetriable(ecf.status) && (
                  <Button
                    disabled={retry.isPending}
                    onClick={() => retry.mutate(ecf.id)}
                  >
                    <RefreshCw /> Reintentar envío
                  </Button>
                )}
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
