/**
 * Convierte una lista de opciones en el mapa `items` que espera `Select`.
 *
 * **Todo `Select` de la aplicación lleva este mapa en la raíz.** No es opcional: sin él,
 * Base UI pinta en el disparador el **valor** en bruto —un `1` donde debería decir
 * «Gastable»— porque no tiene forma de saber qué etiqueta le corresponde. Es distinto de
 * Radix, donde `SelectValue` tomaba el contenido del ítem seleccionado.
 *
 * De paso arregla la navegación por teclado: al teclear, Base UI busca contra estas
 * etiquetas.
 *
 * @example
 * <Select items={selectItems(PRODUCT_CATEGORIES)} value={…}>
 */
export function selectItems(
  options: readonly { value: string; label: string }[],
): Record<string, string> {
  return Object.fromEntries(
    options.map((option) => [option.value, option.label]),
  );
}
