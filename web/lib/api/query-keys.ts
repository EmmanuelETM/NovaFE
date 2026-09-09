/**
 * Fabrica de claves de TanStack Query.
 *
 * Todas las claves salen de aqui, sin excepcion. Es la diferencia entre invalidar la
 * cache con confianza e ir adivinando que cadena se uso en el otro archivo — que es como
 * se pudre un proyecto con TanStack Query.
 *
 * La forma es jerarquica: `users.list(filters)` empieza por el mismo prefijo que
 * `users.all`, asi que invalidar el prefijo alcanza a todas las listas y detalles del
 * modulo de una sola llamada. Los nombres siguen los de los endpoints de la API, para que
 * la clave se pueda adivinar desde la ruta.
 *
 * @example
 * queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
 */

/** Cualquier objeto de filtros de un listado. Entra completo en la clave. */
type Filters = Record<string, unknown>;

export const queryKeys = {
  /** `GET /users/me`: quien soy y que puedo. */
  me: ["me"] as const,

  // Plantilla de un modulo. Copiala tal cual para cada recurso nuevo.
  users: {
    all: ["users"] as const,
    list: (filters: Filters) => ["users", "list", filters] as const,
    detail: (id: string) => ["users", "detail", id] as const,
  },
} as const;
