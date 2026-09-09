import type { FieldValues, Path, UseFormSetError } from "react-hook-form";

import { ApiError } from "./problem";

/**
 * Pega los errores de validación de la API a los campos del formulario.
 *
 * La API los devuelve con el nombre de la propiedad del comando, en **PascalCase**
 * (`Code`, `Name`, `CategoryId`), porque salen del ModelState de ASP.NET. Los campos de
 * react-hook-form son camelCase. La conversión vive aquí y no en cada formulario.
 *
 * Los mensajes se muestran **tal cual**: ya vienen en español y son los mismos que están
 * en el log del servidor. Reescribirlos hace imposible depurar por teléfono.
 *
 * @returns `true` si el error era de validación y quedó repartido por los campos. `false`
 * si es de otra clase y hay que mostrarlo en un toast.
 */
export function applyFieldErrors<TValues extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<TValues>,
): boolean {
  if (!(error instanceof ApiError) || !error.isValidation) return false;

  let applied = false;

  for (const [apiField, message] of Object.entries(error.fieldErrors)) {
    const field = toCamelCase(apiField) as Path<TValues>;

    setError(field, { type: "server", message });
    applied = true;
  }

  return applied;
}

/**
 * `CategoryId` → `categoryId`. Solo la primera letra: el resto ya coincide.
 *
 * Se usa `charAt` y no `name[0]` porque con `noUncheckedIndexedAccess` el indexado da
 * `string | undefined` y obligaría a una aserción. `charAt` devuelve cadena vacía fuera de
 * rango, que es justo el comportamiento que hace falta.
 */
function toCamelCase(name: string): string {
  return name.charAt(0).toLowerCase() + name.slice(1);
}
