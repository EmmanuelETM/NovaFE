import type { components } from "@/lib/api/schema";

/** `GET /api/v1/ops/status` (`OpsStatusDto`). */
export type OpsStatus = components["schemas"]["OpsStatusDto"];

export type WorkerStatus = components["schemas"]["WorkerStatusDto"];
export type OutboxStatus = components["schemas"]["OutboxStatusDto"];
export type SequenceAtRisk = components["schemas"]["SequenceAtRiskDto"];
