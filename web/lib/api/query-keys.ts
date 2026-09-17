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
  /**
   * `GET /users/me`: quien soy y que puedo. `tenantId` (Fase 3) viene de
   * `useParams()` bajo `/tenant/[tenantId]/...` — el mismo usuario tiene un
   * rol distinto por tenant, asi que sin esto la cache mezclaria el rol de
   * un tenant con las pantallas de otro al cambiar de switcher.
   */
  me: (tenantId?: string) => ["me", tenantId ?? null] as const,

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
    all: (tenantId: string) => ["tenant-settings", tenantId] as const,
    list: (tenantId: string) => ["tenant-settings", tenantId, "list"] as const,
    history: (tenantId: string, key: string) =>
      ["tenant-settings", tenantId, "history", key] as const,
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

  /**
   * `GET /ecf`: comprobantes emitidos del contribuyente (self-service).
   * `tenantId` es obligatorio en todas — sin el, cambiar de tenant con el
   * switcher mostraria (por un instante) la cache del tenant anterior.
   */
  ecf: {
    all: (tenantId: string) => ["ecf", tenantId] as const,
    list: (tenantId: string, filters: Filters) =>
      ["ecf", tenantId, "list", filters] as const,
    detail: (tenantId: string, id: string) =>
      ["ecf", tenantId, "detail", id] as const,
  },

  /** `GET /certificates`: certificados del contribuyente actual (self-service). */
  myCertificates: {
    all: (tenantId: string) => ["my-certificates", tenantId] as const,
    list: (tenantId: string) => ["my-certificates", tenantId, "list"] as const,
  },

  /** `GET /sequences`: secuencias de e-NCF del contribuyente actual (self-service). */
  mySequences: {
    all: (tenantId: string) => ["my-sequences", tenantId] as const,
    list: (tenantId: string) => ["my-sequences", tenantId, "list"] as const,
  },

  /** `GET /webhooks`: endpoints de webhook del contribuyente actual (self-service). */
  webhooks: {
    all: (tenantId: string) => ["webhooks", tenantId] as const,
    list: (tenantId: string) => ["webhooks", tenantId, "list"] as const,
  },

  /** `GET /api-keys`: API keys del contribuyente actual (self-service). */
  myApiKeys: {
    all: (tenantId: string) => ["my-api-keys", tenantId] as const,
    list: (tenantId: string) => ["my-api-keys", tenantId, "list"] as const,
  },

  /** `GET /webhooks/{id}/deliveries`: log de entregas de un endpoint (self-service). */
  webhookDeliveries: {
    all: (tenantId: string, endpointId: string) =>
      ["webhook-deliveries", tenantId, endpointId] as const,
    list: (tenantId: string, endpointId: string, filters: Filters) =>
      ["webhook-deliveries", tenantId, endpointId, "list", filters] as const,
  },

  /** `GET /audit-log`: registro de auditoría del contribuyente actual (self-service, RF-14.4). */
  myAuditLog: {
    all: (tenantId: string) => ["my-audit-log", tenantId] as const,
    list: (tenantId: string, filters: Filters) =>
      ["my-audit-log", tenantId, "list", filters] as const,
  },

  /** `GET /emitter-profile`: perfil fiscal del emisor del contribuyente actual (self-service). */
  myEmitterProfile: (tenantId: string) =>
    ["my-emitter-profile", tenantId] as const,

  /** `GET /organizations/{id}/...`: self-service de organizacion (Fase 3). */
  organizations: {
    all: ["organizations"] as const,
    detail: (id: string) => ["organizations", "detail", id] as const,
    members: (id: string) => ["organizations", "members", id] as const,
    tenants: (id: string) => ["organizations", "tenants", id] as const,
  },
} as const;
