"use client";

import { useQuery } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { queryKeys } from "@/lib/api/query-keys";

import { roleRank, type RoleLevel } from "./roles";

/**
 * El perfil que devuelve `GET /api/v1/users/me` (`UserProfileDto`). De acá sale la
 * navegación por rol; `role` es uno de `consultor` | `emisor` | `admin_tenant` |
 * `admin_sistema` (ver `./roles`), y `tenantId` en `null` = operador del SaaS.
 */
export type CurrentUser = components["schemas"]["UserProfileDto"];

/**
 * Quién soy y qué puedo hacer.
 *
 * Es lo primero que se necesita después del login, y lo consultan varias pantallas, así
 * que se cachea largo: el rol de alguien no cambia mientras usa el sistema. Si un
 * administrador se lo cambia, la API lo aplica de inmediato del lado del servidor — lo que
 * quedaría desfasado es solo qué botones se ven, hasta el siguiente refresco.
 */
export function useCurrentUser() {
  return useQuery({
    queryKey: queryKeys.me,
    queryFn: () => api.get<CurrentUser>("/users/me"),
    staleTime: 5 * 60_000,
  });
}

/**
 * Si el usuario alcanza el rango pedido.
 *
 * Devuelve `undefined` mientras no se sabe, y eso es a propósito: con `false` los botones
 * aparecerían al terminar la consulta, y un botón que se materializa medio segundo después
 * se siente como un fallo. Quien lo use debe tratar `undefined` como «todavía no».
 *
 * **Esconder no es proteger.** La API valida cada petición contra `platform_users`;
 * esto solo evita mostrar botones que van a fallar.
 */
export function useCan(minimum: RoleLevel): boolean | undefined {
  const { data, isPending } = useCurrentUser();

  if (isPending) return undefined;

  return roleRank(data?.role) >= minimum;
}
