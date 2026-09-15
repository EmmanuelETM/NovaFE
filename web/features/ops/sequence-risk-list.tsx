import { SEQUENCE_CRIT_REMAINING } from "./hero";
import type { SequenceAtRisk } from "./types";

interface SequenceRiskListProps {
  tenants: SequenceAtRisk[];
}

/**
 * Inventario, no trabajo pendiente — por eso no comparte tarjeta con las
 * colas. En un sistema multitenant un agregado ("4 activas, 1 baja") no le
 * dice a nadie qué hacer: no identifica al tenant. La lista nombra
 * exactamente a quién avisarle. Ya viene ordenada por menor stock restante
 * (misma definición de "bajo stock" que usa el propio tenant).
 */
export function SequenceRiskList({ tenants }: SequenceRiskListProps) {
  if (tenants.length === 0) {
    return (
      <p className="quiet-line">
        Ningún tenant tiene secuencias por agotarse esta semana.
      </p>
    );
  }

  return (
    <div className="tenant-list">
      {tenants.map((item) => {
        const remaining = Number(item.remaining);
        const severity = remaining < SEQUENCE_CRIT_REMAINING ? "crit" : "warn";
        return (
          <div className="tenant-row" key={`${item.tenantName}-${item.type}`}>
            <span className="tenant-row__name">{item.tenantName}</span>
            <span className="tenant-row__type">{item.type}</span>
            <span
              className={`tenant-row__remaining tenant-row__remaining--${severity}`}
            >
              {remaining.toLocaleString("es-DO")} restantes
            </span>
          </div>
        );
      })}
    </div>
  );
}
