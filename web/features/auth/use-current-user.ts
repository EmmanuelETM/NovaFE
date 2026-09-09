"use client";

import { useQuery } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

import type { RoleLevel } from "./roles";

/**
 * El perfil que devuelve `GET /users/me`.
 *
 * Se escribe a mano aqui a proposito: es el unico contrato que el esqueleto de la
 * aplicacion necesita para pintarse. En cuanto exista el documento OpenAPI de tu API,
 * cambialo por el tipo generado:
 *
 *     import type { components } from "@/lib/api/schema";
 *     export type CurrentUser = components["schemas"]["UserProfileDto"];
 */
export interface CurrentUser {
  id: string;
  /** El identificador con el que entra. Suele ser el correo. */
  userName: string;
  /** El nombre real, si se conoce. */
  displayName?: string | null;
  /** El nivel del rol. Ver `ROLE` en `./roles`. */
  roleId: number;
  /** El nombre del rol, para mostrar. */
  roleLabel?: string | null;
}

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
 * Si el usuario alcanza el nivel pedido.
 *
 * Devuelve `undefined` mientras no se sabe, y eso es a propósito: con `false` los botones
 * aparecerían al terminar la consulta, y un botón que se materializa medio segundo después
 * se siente como un fallo. Quien lo use debe tratar `undefined` como «todavía no».
 *
 * **Esconder no es proteger.** La API valida cada petición contra su propia tabla de
 * perfiles; esto solo evita mostrar botones que van a fallar.
 */
export function useCan(minimum: RoleLevel): boolean | undefined {
  const { data, isPending } = useCurrentUser();

  if (isPending) return undefined;

  return (data?.roleId ?? 0) >= minimum;
}
