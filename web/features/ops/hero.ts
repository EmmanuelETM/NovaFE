import { formatDuration, formatRelative, parseTimeSpan } from "./format";
import type { OpsStatus, OutboxStatus, WorkerStatus } from "./types";

/** El nombre técnico del worker (clave de `heartbeat.Beat`) a una etiqueta legible. */
export const WORKER_LABELS: Record<string, string> = {
  "ecf-submission": "Envío a la DGII",
  "webhook-delivery": "Entrega de webhooks",
  "expiry-monitor": "Monitor de vencimientos",
  retention: "Retención y purgas",
  "settings-poller": "Poller de settings",
};

export const WARN_FRACTION = 0.8;

/** Backlog más viejo que esto ya no es "trabajo en curso normal" — es un atasco. */
const STUCK_BACKLOG_MS = 5 * 60 * 1000;

/** Por debajo de esto, un tenant no está "bajo" — está prácticamente detenido. */
export const SEQUENCE_CRIT_REMAINING = 50;

export type Severity = "ok" | "warn" | "crit";

const SEVERITY_RANK: Record<Severity, number> = { crit: 2, warn: 1, ok: 0 };

export function workerFraction(worker: WorkerStatus, now: number): number {
  return (
    (now - new Date(worker.lastBeatAt).getTime()) /
    parseTimeSpan(worker.maxSilence)
  );
}

export function workerSeverity(worker: WorkerStatus, now: number): Severity {
  const fraction = workerFraction(worker, now);
  if (!worker.healthy || fraction >= 1) return "crit";
  if (fraction >= WARN_FRACTION) return "warn";
  return "ok";
}

interface QueueConfig {
  key: "dgii" | "webhooks";
  field: "ecfSubmissionOutbox" | "webhookOutbox";
  title: string;
  actorLabel: string;
  deadClause: (n: number) => string;
  stuckClause: (d: string) => string;
  emptyReason: string;
}

export const QUEUES: QueueConfig[] = [
  {
    key: "dgii",
    field: "ecfSubmissionOutbox",
    title: "Envío a la DGII",
    actorLabel: WORKER_LABELS["ecf-submission"] ?? "Envío a la DGII",
    deadClause: (n) => `tiene ${n} comprobante${n === 1 ? "" : "s"} sin salida`,
    stuckClause: (d) => `no avanza hace ${d}`,
    emptyReason: "Todo lo emitido fue aceptado.",
  },
  {
    key: "webhooks",
    field: "webhookOutbox",
    title: "Webhooks",
    actorLabel: WORKER_LABELS["webhook-delivery"] ?? "Entrega de webhooks",
    deadClause: (n) =>
      `tiene ${n} webhook${n === 1 ? "" : "s"} sin poder entregarse`,
    stuckClause: (d) => `no avanza hace ${d}`,
    emptyReason: "Todas las entregas llegaron a destino.",
  },
];

/** Worker cuyo latido y cuya cola son, para quien lee el titular, el mismo sujeto. */
const ACTOR_BY_WORKER: Record<string, string> = {
  "ecf-submission": "dgii",
  "webhook-delivery": "webhooks",
};

interface Actor {
  label: string;
  severity: Severity;
  weight: number;
  clauses: string[];
}

interface Fact {
  subject: string;
  severity: Severity;
  weight: number;
  text: string;
}

/**
 * Barre los tres bloques (workers, colas, secuencias por tenant) y arma un
 * hecho por *actor real*, no por señal suelta: si el mismo actor (p. ej. "el
 * envío a la DGII") aporta más de una señal — su worker no late Y su cola
 * tiene dead-letter —, las funde en una sola cláusula con un solo sujeto, en
 * vez de dejar que cada señal hable por su cuenta y se repita el nombre.
 */
export function buildActionableFacts(data: OpsStatus, now: number): Fact[] {
  const actors = new Map<string, Actor>();

  const addClause = (
    key: string,
    label: string,
    severity: Severity,
    weight: number,
    clause: string,
  ) => {
    const actor = actors.get(key) ?? {
      label,
      severity: "ok" as Severity,
      weight: 0,
      clauses: [],
    };
    actor.clauses.push(clause);
    if (SEVERITY_RANK[severity] > SEVERITY_RANK[actor.severity])
      actor.severity = severity;
    actor.weight = Math.max(actor.weight, weight);
    actors.set(key, actor);
  };

  for (const worker of data.workers) {
    const severity = workerSeverity(worker, now);
    if (severity === "ok") continue;
    const label = WORKER_LABELS[worker.name] ?? worker.name;
    const fraction = workerFraction(worker, now);
    const elapsedMs = now - new Date(worker.lastBeatAt).getTime();
    const clause =
      severity === "crit"
        ? `no responde ${formatRelative(new Date(worker.lastBeatAt).getTime(), now)}`
        : `lleva ${formatDuration(elapsedMs)} sin latir (${Math.round(fraction * 100)}% de su presupuesto)`;
    addClause(
      ACTOR_BY_WORKER[worker.name] ?? `worker:${worker.name}`,
      label,
      severity,
      fraction,
      clause,
    );
  }

  for (const queue of QUEUES) {
    const queueData: OutboxStatus = data[queue.field];
    const dead = Number(queueData.dead);
    const oldestPendingAt = queueData.oldestPendingAt;
    if (dead > 0) {
      addClause(
        queue.key,
        queue.actorLabel,
        "crit",
        1 + dead,
        queue.deadClause(dead),
      );
    } else if (oldestPendingAt) {
      const age = now - new Date(oldestPendingAt).getTime();
      if (age > STUCK_BACKLOG_MS) {
        addClause(
          queue.key,
          queue.actorLabel,
          "warn",
          0.6,
          queue.stuckClause(formatDuration(age)),
        );
      }
    }
  }

  const facts: Fact[] = [];
  for (const [subject, actor] of actors) {
    if (actor.severity === "ok") continue;
    facts.push({
      subject,
      severity: actor.severity,
      weight: actor.weight,
      text: `${actor.label} ${actor.clauses.join(" y ")}`,
    });
  }

  if (data.sequencesAtRisk.length > 0) {
    const worst = [...data.sequencesAtRisk].sort(
      (a, b) => Number(a.remaining) - Number(b.remaining),
    )[0];
    if (worst) {
      const severity: Severity =
        Number(worst.remaining) < SEQUENCE_CRIT_REMAINING ? "crit" : "warn";
      facts.push({
        subject: "sequences",
        severity,
        weight: severity === "crit" ? 1.5 : 0.3,
        text: `${worst.tenantName} se está quedando sin secuencia ${worst.type}`,
      });
    }
  }

  facts.sort((a, b) => {
    if (SEVERITY_RANK[a.severity] !== SEVERITY_RANK[b.severity]) {
      return SEVERITY_RANK[b.severity] - SEVERITY_RANK[a.severity];
    }
    return b.weight - a.weight;
  });

  return facts;
}

export interface Hero {
  severity: Severity;
  headline: string;
  support: string;
}

/**
 * Máximo dos cláusulas en la oración, no dos hechos: un actor ya fundido
 * (worker + cola) puede llegar con dos cláusulas propias, y en ese caso ya
 * no hay lugar para un segundo hecho sin volverse un párrafo.
 */
export function heroFor(data: OpsStatus, now: number): Hero {
  const facts = buildActionableFacts(data, now);

  if (facts.length === 0) {
    return {
      severity: "ok",
      headline: "Todo en orden",
      support: `${data.workers.length} workers latiendo dentro de su presupuesto, ambas colas vacías y sin secuencias por agotarse.`,
    };
  }

  const chosen: Fact[] = [];
  const usedSubjects = new Set<string>();
  let clauseBudget = 2;
  for (const fact of facts) {
    if (clauseBudget <= 0) break;
    if (usedSubjects.has(fact.subject)) continue;
    const clauseCount = fact.text.split(" y ").length;
    if (clauseCount > clauseBudget) continue;
    chosen.push(fact);
    usedSubjects.add(fact.subject);
    clauseBudget -= clauseCount;
  }

  const sentence = chosen.map((fact) => fact.text).join(" y ");
  const support = sentence.charAt(0).toUpperCase() + sentence.slice(1) + ".";

  const worst = facts[0];

  return {
    severity: worst ? worst.severity : "ok",
    headline:
      worst?.severity === "crit"
        ? "Hay documentos detenidos"
        : "Algo necesita tu atención",
    support,
  };
}
