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

  /** `GET /platform-settings`: la config operativa de la plataforma (operador). */
  platformSettings: {
    all: ["platform-settings"] as const,
    list: () => ["platform-settings", "list"] as const,
    history: (key: string) => ["platform-settings", "history", key] as const,
  },

  /** `GET /settings`: la config self-serve del contribuyente. */
  tenantSettings: {
    all: ["tenant-settings"] as const,
    list: () => ["tenant-settings", "list"] as const,
    history: (key: string) => ["tenant-settings", "history", key] as const,
  },

  /** Usuarios del dashboard (operador): operadores del SaaS y empleados por contribuyente. */
  platformUsers: {
    all: ["platform-users"] as const,
    operators: () => ["platform-users", "operators"] as const,
    byTenant: (tenantId: string) =>
      ["platform-users", "by-tenant", tenantId] as const,
  },

  /** `GET /tenants`: contribuyentes (para el selector, el listado y el detalle). */
  tenants: {
    all: ["tenants"] as const,
    options: () => ["tenants", "options"] as const,
    list: (filters: Filters) => ["tenants", "list", filters] as const,
    detail: (id: string) => ["tenants", "detail", id] as const,
    certificates: (id: string) => ["tenants", "certificates", id] as const,
    sequences: (id: string) => ["tenants", "sequences", id] as const,
    apiKeys: (id: string) => ["tenants", "api-keys", id] as const,
  },

  /** `GET /ops/status`: latido de workers, outbox y secuencias (operador). */
  ops: {
    all: ["ops"] as const,
    status: () => ["ops", "status"] as const,
  },

  /** `GET /ecf`: comprobantes emitidos del contribuyente (self-service). */
  ecf: {
    all: ["ecf"] as const,
    list: (filters: Filters) => ["ecf", "list", filters] as const,
    detail: (id: string) => ["ecf", "detail", id] as const,
  },

  /** `GET /certificates`: certificados del contribuyente actual (self-service). */
  myCertificates: {
    all: ["my-certificates"] as const,
    list: () => ["my-certificates", "list"] as const,
  },

  /** `GET /sequences`: secuencias de e-NCF del contribuyente actual (self-service). */
  mySequences: {
    all: ["my-sequences"] as const,
    list: () => ["my-sequences", "list"] as const,
  },

  /** `GET /webhooks`: endpoints de webhook del contribuyente actual (self-service). */
  webhooks: {
    all: ["webhooks"] as const,
    list: () => ["webhooks", "list"] as const,
  },
} as const;
