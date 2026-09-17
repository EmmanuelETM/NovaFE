import type { components } from "@/lib/api/schema";

/** Un comprobante emitido, con el detalle completo (`EcfDto`). */
export type Ecf = components["schemas"]["EcfDto"];

/** Fila del listado de comprobantes (`EcfSummaryDto`). */
export type EcfSummary = components["schemas"]["EcfSummaryDto"];

export type EcfPage = components["schemas"]["PagedResultOfEcfSummaryDto"];

/**
 * Etiqueta corta por tipo de e-CF, para una columna de tabla — el
 * `DisplayName` completo del backend ("Factura de Crédito Fiscal
 * Electrónica") no entra. Los 10 tipos, mismo orden que `EcfType` en
 * `src/Domain/Common/EcfType.cs`.
 */
export const ECF_TYPE_LABELS: Record<number, string> = {
  31: "31 · Crédito Fiscal",
  32: "32 · Consumo",
  33: "33 · Nota de Débito",
  34: "34 · Nota de Crédito",
  41: "41 · Compras",
  43: "43 · Gastos Menores",
  44: "44 · Regímenes Especiales",
  45: "45 · Gubernamental",
  46: "46 · Exportaciones",
  47: "47 · Pagos al Exterior",
};

/** La etiqueta de un tipo; el propio código si no se reconoce. */
export function ecfTypeLabel(type: number | string): string {
  const code = Number(type);
  return ECF_TYPE_LABELS[code] ?? String(type);
}

/**
 * Etiquetas de estado, mismos 7 valores que `EcfStatus.PublicName` en
 * `src/Domain/Ecf/EcfStatus.cs`.
 */
export const ECF_STATUS_LABELS: Record<string, string> = {
  signed: "Firmado",
  submitted: "Enviado",
  accepted: "Aceptado",
  accepted_conditional: "Aceptado condicional",
  rejected: "Rechazado",
  review: "En revisión",
  failed: "Fallido",
};

/** La etiqueta de un estado; el propio valor si no se reconoce. */
export function ecfStatusLabel(status: string): string {
  return ECF_STATUS_LABELS[status] ?? status;
}

/**
 * La variante de `Badge` para un estado — sin colores nuevos, las 4 que ya
 * existen (`default`/`secondary`/`destructive`/`outline`).
 */
export function ecfStatusVariant(
  status: string,
): "default" | "secondary" | "destructive" {
  if (status === "accepted" || status === "accepted_conditional")
    return "default";
  if (status === "rejected" || status === "failed") return "destructive";
  return "secondary";
}

/** Si el comprobante se puede reencolar (`POST /ecf/{id}/retry`). */
export function isRetriable(status: string): boolean {
  return status === "failed" || status === "review";
}
