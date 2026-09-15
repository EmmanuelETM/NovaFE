/**
 * Formato de tiempo del mostrador de operación: no vive en `lib/format`
 * porque nada más lo necesita — acá todo se lee como duración relativa
 * ("hace 12 s", "2 min / 3 min"), no como fecha exacta.
 */

/** `"00:03:00"` (el `TimeSpan` de .NET) → milisegundos. */
export function parseTimeSpan(value: string): number {
  const [h = "0", m = "0", s = "0"] = value.split(":");
  return ((Number(h) * 60 + Number(m)) * 60 + Number(s)) * 1000;
}

/** ms → "3 min", "18 h", "45 s" — para duraciones sueltas. */
export function formatDuration(ms: number): string {
  const totalSeconds = Math.round(ms / 1000);
  if (totalSeconds < 60) return `${totalSeconds} s`;
  const totalMinutes = Math.round(totalSeconds / 60);
  if (totalMinutes < 60) return `${totalMinutes} min`;
  const hours = Math.round(totalMinutes / 60);
  return `${hours} h`;
}

/** instante (ms) → "hace 12 s" / "hace 3 min" — para el header y los latidos rotos. */
export function formatRelative(atMs: number, now: number): string {
  const ms = Math.max(0, now - atMs);
  if (ms < 1000) return "recién";
  return `hace ${formatDuration(ms)}`;
}

/**
 * "2 min / 3 min" — el silencio actual sobre el presupuesto máximo, en la
 * misma unidad. Expone el denominador: el número sigue siendo útil incluso
 * para quien está ajustando `maxSilence`.
 */
export function formatBudgetPair(
  elapsedMs: number,
  maxSilenceMs: number,
): string {
  const unit =
    maxSilenceMs < 60_000 ? "s" : maxSilenceMs < 3_600_000 ? "min" : "h";
  const divisor = unit === "s" ? 1000 : unit === "min" ? 60_000 : 3_600_000;
  const fmt = (ms: number) => {
    const value = ms / divisor;
    return value > 0 && value < 1 ? "<1" : Math.round(value);
  };
  return `${fmt(elapsedMs)} ${unit} / ${fmt(maxSilenceMs)} ${unit}`;
}

export function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
