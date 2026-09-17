import type { components } from "@/lib/api/schema";

/** `GET /api/v1/ops/status` (`OpsStatusDto`). */
export type OpsStatus = components["schemas"]["OpsStatusDto"];

export type WorkerStatus = components["schemas"]["WorkerStatusDto"];
export type OutboxStatus = components["schemas"]["OutboxStatusDto"];
export type SequenceAtRisk = components["schemas"]["SequenceAtRiskDto"];

/** `GET /api/v1/ops/dead-deliveries` (`DeadWebhookDeliveryDto`): entregas de webhook muertas en toda la plataforma. */
export type DeadWebhookDelivery =
  components["schemas"]["DeadWebhookDeliveryDto"];

export type DeadWebhookDeliveryPage =
  components["schemas"]["PagedResultOfDeadWebhookDeliveryDto"];
