import type { components } from "@/lib/api/schema";

export type {
  ApiKey,
  ApiKeyCreated,
  Certificate,
  NcfSequence,
} from "@/features/tenants/types";

/** Un endpoint de webhook (`WebhookEndpointDto`). Nunca incluye el `secret`. */
export type WebhookEndpoint = components["schemas"]["WebhookEndpointDto"];

/** Respuesta de la creación: el endpoint más el `secret` en claro (única vez). */
export type WebhookEndpointCreated =
  components["schemas"]["WebhookEndpointCreatedDto"];

/** Respuesta de rotar el secret: el nuevo `secret` en claro. */
export type WebhookSecret = components["schemas"]["WebhookSecretDto"];

/** Resultado de un `ping`. */
export type WebhookPingResult = components["schemas"]["WebhookPingResultDto"];

/** Una fila del log de entregas de un webhook (`WebhookDeliveryDto`). */
export type WebhookDelivery = components["schemas"]["WebhookDeliveryDto"];

export type WebhookDeliveryPage =
  components["schemas"]["PagedResultOfWebhookDeliveryDto"];

/** Una fila del registro de auditoría (RF-14.4, `AuditLogEntryDto`). */
export type AuditLogEntry = components["schemas"]["AuditLogEntryDto"];

export type AuditLogPage =
  components["schemas"]["PagedResultOfAuditLogEntryDto"];
