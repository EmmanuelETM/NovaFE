/**
 * Los roles de la aplicación, con su nivel.
 *
 * Son **jerárquicos**: el número es el nivel, y cada uno incluye lo del anterior. Eso es
 * lo que permite que `lib/navigation.ts` pida un mínimo (`minRole`) en vez de enumerar
 * roles, y que `<Can role={ROLE.supervisor}>` alcance también al administrador.
 *
 * Los valores deben ser los mismos que devuelve la API en `roleId`.
 */
export const ROLE = {
  operator: 1,
  supervisor: 2,
  administrator: 3,
} as const;

export type RoleLevel = (typeof ROLE)[keyof typeof ROLE];
