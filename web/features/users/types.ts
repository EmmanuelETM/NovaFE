import type { components } from "@/lib/api/schema";

/** Un usuario del dashboard (`PlatformUserDto`). */
export type PlatformUser = components["schemas"]["PlatformUserDto"];

/** Fila del listado de contribuyentes para el selector (`TenantSummaryDto`). */
export type TenantSummary = components["schemas"]["TenantSummaryDto"];

/**
 * Vigente = no revocado. La API **omite** los campos null, así que en un usuario
 * activo `revokedAt` llega `undefined` (no `null`) y `isActive` puede no venir —
 * de ahí el fallback a la ausencia de `revokedAt`.
 */
export function isActive(user: PlatformUser): boolean {
  return user.isActive ?? !user.revokedAt;
}
