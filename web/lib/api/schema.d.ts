/**
 * Marcador de posición. **Este archivo se GENERA** desde el documento OpenAPI de la API:
 *
 *     bun run api:types
 *
 * Ajusta primero la URL del script `api:types` en `package.json`. Mientras no se haya
 * generado, `paths` y `components` están vacíos y `lib/api/paths.ts` no resuelve ninguna
 * ruta — que es lo correcto: mejor un tipo vacío que uno inventado.
 */
export interface paths {}

export interface components {
  schemas: Record<string, unknown>;
}

export interface operations {}
