import type { Severity } from "./hero";

/** El punto que titila junto al titular, coloreado por la severidad del héroe. */
export function StatusDot({ severity }: { severity: Severity }) {
  return (
    <span className={`status-dot status-dot--${severity}`} aria-hidden="true">
      <span className="status-dot__ping" />
      <span className="status-dot__core" />
    </span>
  );
}
