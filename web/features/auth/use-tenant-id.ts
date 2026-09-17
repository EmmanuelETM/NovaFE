"use client";

import { useParams } from "next/navigation";

/**
 * El tenant activo, sacado de la URL (`/tenant/[tenantId]/...`) — nunca de
 * una cookie ni de un estado global, para que dos pestañas en dos tenants
 * distintos no se pisen (ver `docs` de Fase 3 / `lib/api/server.ts`).
 *
 * Solo se usa desde hooks de datos que viven **dentro** de esa ruta —
 * `use-ecf.ts`, `tenant-config/*`, `tenant-settings/*`. Si se usa fuera de
 * `/tenant/[tenantId]/...`, `tenantId` viene `undefined` en tiempo de
 * ejecución aunque el tipo diga `string`; es responsabilidad de quien
 * importa este hook no hacerlo.
 */
export function useTenantId(): string {
  const { tenantId } = useParams<{ tenantId: string }>();
  return tenantId;
}
