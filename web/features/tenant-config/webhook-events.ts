/**
 * Los eventos a los que un endpoint de webhook se puede suscribir, agrupados
 * para el selector de alta/edición. Mismos 15 valores exactos que
 * `WebhookEventType.Subscribable` en `src/Domain/Webhooks/WebhookEventType.cs`
 * — sin comodines (`ecf.*`, `*`), que la API sí acepta pero esta pantalla no
 * ofrece (decisión: mantener el selector simple y explícito).
 */
export interface WebhookEventOption {
  value: string;
  label: string;
}

export interface WebhookEventGroup {
  label: string;
  events: WebhookEventOption[];
}

export const WEBHOOK_EVENT_GROUPS: WebhookEventGroup[] = [
  {
    label: "Ciclo de vida del e-CF",
    events: [
      { value: "ecf.submitted", label: "Enviado a la DGII" },
      { value: "ecf.accepted", label: "Aceptado" },
      { value: "ecf.accepted_conditional", label: "Aceptado condicional" },
      { value: "ecf.rejected", label: "Rechazado" },
      { value: "ecf.review", label: "En revisión" },
      { value: "ecf.failed", label: "Fallido" },
      { value: "ecf.duplicate_suspected", label: "Posible duplicado" },
    ],
  },
  {
    label: "Certificados",
    events: [
      { value: "certificate.expiring", label: "Por vencer" },
      { value: "certificate.expired", label: "Vencido" },
    ],
  },
  {
    label: "Secuencias de e-NCF",
    events: [
      { value: "sequence.low", label: "Quedan pocos" },
      { value: "sequence.exhausted", label: "Agotada" },
      { value: "sequence.expiring", label: "Por vencer" },
      { value: "sequence.expired", label: "Vencida" },
    ],
  },
  {
    label: "Contingencia",
    events: [
      { value: "contingency.activated", label: "Activada" },
      { value: "contingency.deactivated", label: "Desactivada" },
    ],
  },
];

const LABELS: Record<string, string> = Object.fromEntries(
  WEBHOOK_EVENT_GROUPS.flatMap((group) =>
    group.events.map((event) => [event.value, event.label]),
  ),
);

/** La etiqueta de un evento; el propio valor si no se reconoce (comodines incluidos). */
export function webhookEventLabel(value: string): string {
  return LABELS[value] ?? value;
}
