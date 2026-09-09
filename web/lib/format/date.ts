/**
 * Formato de fechas.
 *
 * **La zona horaria va explicita.** La API devuelve `DateTimeOffset` en UTC
 * (`2026-08-21T22:38:41+00:00`), y sin fijar la zona el navegador usa la suya. Eso
 * importa mas de lo que parece: una venta de las 8 de la noche cae al dia siguiente en
 * UTC, asi que el cuadre diario mostraria la venta en el dia equivocado. Republica
 * Dominicana es UTC-4 sin horario de verano.
 */

const ZONE = "America/Santo_Domingo";

const dateAndTime = new Intl.DateTimeFormat("es-DO", {
  timeZone: ZONE,
  dateStyle: "medium",
  timeStyle: "short",
});

const dateOnly = new Intl.DateTimeFormat("es-DO", {
  timeZone: ZONE,
  dateStyle: "medium",
});

const timeOnly = new Intl.DateTimeFormat("es-DO", {
  timeZone: ZONE,
  timeStyle: "short",
});

const isoDay = new Intl.DateTimeFormat("en-CA", {
  timeZone: ZONE,
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
});

/** `"2026-08-21T22:38:41+00:00"` → `"21 ago 2026, 6:38 p. m."` */
export function formatDateTime(iso: string): string {
  return dateAndTime.format(new Date(iso));
}

export function formatDate(iso: string): string {
  return dateOnly.format(new Date(iso));
}

export function formatTime(iso: string): string {
  return timeOnly.format(new Date(iso));
}

/**
 * El dia local de una marca de tiempo, como `YYYY-MM-DD`. Es lo que hay que usar para
 * agrupar por dia: hacerlo con la fecha UTC pondria las ventas de la noche en el dia
 * siguiente.
 */
export function localDay(iso: string): string {
  return isoDay.format(new Date(iso));
}

/**
 * Desplazamiento fijo de República Dominicana. **No hay horario de verano**, así que es una
 * constante y no una consulta a la biblioteca de zonas.
 */
const OFFSET = "-04:00";

/** El día de hoy en Santo Domingo, como `YYYY-MM-DD`. */
export function today(): string {
  return localDay(new Date().toISOString());
}

/**
 * Suma (o resta) días a un día local.
 *
 * El cálculo se ancla al mediodía a propósito: hacerlo a medianoche deja el instante justo
 * en la frontera del día, y cualquier redondeo lo empuja al día vecino.
 */
export function addDays(day: string, days: number): string {
  const base = new Date(`${day}T12:00:00${OFFSET}`);

  base.setUTCDate(base.getUTCDate() + days);

  return localDay(base.toISOString());
}

/**
 * El instante inicial y final de un rango de días locales, listo para mandarlo a la API.
 *
 * Se manda con el desplazamiento explícito y no en UTC porque así la consulta dice lo que
 * significa: «desde el principio del 26 en Santo Domingo». Convertirlo a `04:00Z` funciona
 * igual, pero al leerlo en un log nadie sabe si es un acierto o un error de zona.
 */
export function dayRange(
  from: string,
  to: string,
): { from: string; to: string } {
  return {
    from: `${from}T00:00:00${OFFSET}`,
    to: `${to}T23:59:59${OFFSET}`,
  };
}
