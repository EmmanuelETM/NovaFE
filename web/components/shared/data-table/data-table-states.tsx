"use client";

import { AlertCircle, Inbox } from "lucide-react";

import { Skeleton } from "@/components/ui/skeleton";
import { TableCell, TableRow } from "@/components/ui/table";
import { ApiError } from "@/lib/api/problem";

import type { DataTableEmptyState } from "./types";

/**
 * Esqueleto con la forma del contenido, no un spinner centrado. La diferencia es que la
 * página no salta cuando llegan los datos.
 */
export function DataTableSkeletonRows({
  columns,
  rows,
}: {
  columns: number;
  rows: number;
}) {
  return (
    <>
      {Array.from({ length: rows }, (_, rowIndex) => (
        <TableRow key={rowIndex} className="hover:bg-transparent">
          {Array.from({ length: columns }, (_, cellIndex) => (
            <TableCell key={cellIndex}>
              <Skeleton className="h-4 w-full max-w-[160px]" />
            </TableCell>
          ))}
        </TableRow>
      ))}
    </>
  );
}

/** Un listado vacío siempre dice qué es y ofrece la salida. */
export function DataTableEmpty({
  columns,
  state,
}: {
  columns: number;
  state: DataTableEmptyState;
}) {
  return (
    <TableRow className="hover:bg-transparent">
      <TableCell colSpan={columns} className="h-48">
        <div className="flex flex-col items-center justify-center gap-2 text-center">
          <Inbox className="text-muted-foreground/50 size-8" aria-hidden />
          <p className="font-medium">{state.title}</p>
          {state.description && (
            <p className="text-muted-foreground max-w-sm text-sm">
              {state.description}
            </p>
          )}
          {state.action && <div className="mt-2">{state.action}</div>}
        </div>
      </TableCell>
    </TableRow>
  );
}

/**
 * El error, con el mensaje que ya viene en español desde la API. El `traceId` solo en los
 * inesperados: en un 403 no aporta nada y ocupa espacio.
 */
export function DataTableError({
  columns,
  error,
}: {
  columns: number;
  error: unknown;
}) {
  const apiError = error instanceof ApiError ? error : null;

  const message =
    apiError?.message ??
    (error instanceof Error
      ? error.message
      : "No se pudo cargar la información.");

  const traceId =
    apiError && apiError.status >= 500 ? apiError.problem.traceId : undefined;

  return (
    <TableRow className="hover:bg-transparent">
      <TableCell colSpan={columns} className="h-48">
        <div className="flex flex-col items-center justify-center gap-2 text-center">
          <AlertCircle className="text-destructive/70 size-8" aria-hidden />
          <p className="font-medium">{message}</p>
          {traceId && (
            <p className="text-muted-foreground font-mono text-xs">
              traceId: {traceId}
            </p>
          )}
        </div>
      </TableCell>
    </TableRow>
  );
}
