/**
 * Tipos derivados del documento OpenAPI de la API.
 *
 * `schema.d.ts` se GENERA — no se edita a mano. Para regenerarlo, con la API corriendo:
 *
 *     bun run api:types
 *
 * Que los tipos vengan del contrato y no escritos a mano es lo que convierte un nombre
 * de campo mal puesto en un error de compilacion en vez de un `undefined` en pantalla.
 */
import type { paths } from "./schema";

/** Todas las rutas de la API, tipadas. */
export type ApiPaths = paths;

/** El cuerpo de respuesta 200 de un GET, dada su ruta. */
export type GetResponse<P extends keyof ApiPaths> = ApiPaths[P] extends {
  get: { responses: { 200: { content: { "application/json": infer R } } } };
}
  ? R
  : never;

/** El cuerpo de peticion de un POST, dada su ruta. */
export type PostBody<P extends keyof ApiPaths> = ApiPaths[P] extends {
  post: { requestBody?: { content: { "application/json": infer B } } };
}
  ? B
  : never;
