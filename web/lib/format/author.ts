/**
 * El autor de un cambio, listo para mostrar.
 *
 * La convención que asume el scaffold: la API resuelve el nombre en la propia consulta y
 * devuelve dos campos por cada asiento de bitácora, el identificador (`changedBy`) y el
 * nombre (`changedByName`). **La UI muestra el nombre; el identificador no se muestra
 * nunca** —un GUID en pantalla no responde «quién», que es justo la pregunta que la
 * bitácora existe para contestar—.
 *
 * El nombre viene nulo en dos casos, y los dos significan lo mismo para quien lee: la fila
 * no tiene autor. Pasa con lo que sembró el arranque del sistema y con lo que se registró
 * antes de que existiera la autenticación.
 */
const SIN_AUTOR = "el sistema";

export function formatAuthor(name: string | null | undefined): string {
  const limpio = name?.trim();

  return limpio ? formatUserName(limpio) : SIN_AUTOR;
}

/**
 * El nombre corto de alguien: `etorres@ejemplo.com` → `etorres`.
 *
 * **Se deriva, no se guarda.** Un tercer campo que casi siempre sería «el correo sin el
 * dominio» es un dato que puede quedar desincronizado sin dar nada a cambio.
 *
 * Lo que no es un correo pasa tal cual, y eso es lo que hace que sirva para todo: quien
 * tenga su nombre real puesto se ve completo, y quien no, corto. El correo completo sigue
 * estando en el menú del usuario.
 */
export function formatUserName(value: string): string {
  const arroba = value.indexOf("@");

  return arroba > 0 ? value.slice(0, arroba) : value;
}
