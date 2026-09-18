import type { components } from "@/lib/api/schema";

/** El resumen fiscal de un rango de fechas (`GET /finance/summary`). */
export type FiscalSummary = components["schemas"]["FiscalSummaryDto"];

/** Los totales de un tipo de e-CF dentro del resumen. */
export type FiscalSummaryByType =
  components["schemas"]["FiscalSummaryByTypeDto"];
