/**
 * Formato de dinero. Peso dominicano, locale `es-DO`. Cambia `currency` y el locale si la
 * aplicacion opera en otra moneda.
 *
 * **La UI no calcula dinero.** Los importes vienen calculados por la API —subtotal, total,
 * impuestos, diferencias— y aqui solo se formatean. Duplicar una regla de precios en
 * TypeScript garantiza que las dos versiones se separen con el tiempo.
 */

const currency = new Intl.NumberFormat("es-DO", {
  style: "currency",
  currency: "DOP",
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/** `1234.5` → `"RD$1,234.50"` */
export function formatMoney(value: number): string {
  return currency.format(value);
}

const integer = new Intl.NumberFormat("es-DO", { maximumFractionDigits: 0 });

/** Cantidades y contadores: `1234` → `"1,234"` */
export function formatCount(value: number): string {
  return integer.format(value);
}
