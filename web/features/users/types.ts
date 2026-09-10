import type { components } from "@/lib/api/schema";

/** Un usuario del dashboard (`PlatformUserDto`). */
export type PlatformUser = components["schemas"]["PlatformUserDto"];

/** Fila del listado de contribuyentes para el selector (`TenantSummaryDto`). */
export type TenantSummary = components["schemas"]["TenantSummaryDto"];

/** Vigente = no revocado. `isActive` viene calculado de la API, pero derivarlo evita el opcional. */
export function isActive(user: PlatformUser): boolean {
  return user.revokedAt === null;
}
