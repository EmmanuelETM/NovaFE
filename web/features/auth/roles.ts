/**
 * Los roles del dashboard, con su rango.
 *
 * Son los 4 que devuelve la API en `role` (`GET /users/me`). El rango los hace
 * comparables: `lib/navigation.ts` pide un mínimo (`minRole`) y la nav sale por
 * `roleRank(user.role) >= item.minRole`.
 *
 * - `consultor` — solo lectura.
 * - `emisor` — emite y consulta lo propio.
 * - `admin_tenant` — configuración del contribuyente, certificados, secuencias, usuarios.
 * - `admin_sistema` — operador del SaaS. Rango más alto (ve todo lo de un `admin_tenant`;
 *   las pantallas propias de operador son otro asunto).
 *
 * **Esconder no es proteger**: la API valida cada petición contra `platform_users`.
 */
export const ROLE = {
  consultor: 1,
  emisor: 2,
  admin_tenant: 3,
  admin_sistema: 4,
} as const;

export type RoleName = keyof typeof ROLE;
export type RoleLevel = (typeof ROLE)[keyof typeof ROLE];

export const ROLE_LABELS: Record<RoleName, string> = {
  consultor: "Consultor",
  emisor: "Emisor",
  admin_tenant: "Administrador",
  admin_sistema: "Operador",
};

/** El nombre de rol de la API → su rango. Desconocido o ausente = 0 (no ve nada). */
export function roleRank(role: string | null | undefined): number {
  return role != null && role in ROLE ? ROLE[role as RoleName] : 0;
}

/** La etiqueta legible de un rol; el propio valor si no se reconoce. */
export function roleLabel(role: string | null | undefined): string {
  return role != null && role in ROLE
    ? ROLE_LABELS[role as RoleName]
    : "Sin rol";
}
