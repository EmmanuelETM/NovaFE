import { createEnv } from "@t3-oss/env-nextjs";
import { z } from "zod";

/**
 * Configuración del servidor, validada al arrancar.
 *
 * Se usa `@t3-oss/env-nextjs` en lugar de leer `process.env` desperdigado. Aporta cuatro
 * cosas concretas sobre un esquema de Zod a mano:
 *
 * 1. Separa `server` de `client` estructuralmente. Aquí no hay variables de cliente: nada
 *    de esto puede llegar al navegador, y leer una de estas desde un componente de cliente
 *    revienta con un mensaje claro en vez de devolver `undefined` en silencio.
 * 2. `emptyStringAsUndefined` trata `VAR=` como ausente. Sin eso, copiar `.env.example`
 *    —que trae las variables vacías a propósito— rompe el arranque, porque una cadena
 *    vacía **es** una cadena y `z.uuid()` la rechaza.
 * 3. `skipValidation` permite compilar sin entorno, que es lo que hace falta en una imagen
 *    de Docker donde las variables llegan al ejecutar y no al construir.
 * 4. El tipo queda estrecho: `string`, no `string | undefined`, en todo lo que tiene default.
 */
export const env = createEnv({
  server: {
    /** Base de la API NovaFE. Sin barra final. En local: http://localhost:5071. */
    APP_API_URL: z.url().default("http://localhost:5071"),

    /** NovaFE versiona por ruta: `/api/v1/...`. */
    APP_API_VERSION: z.string().min(1).default("v1"),

    /**
     * Identidad de desarrollo: el id del contribuyente con el que se actúa.
     *
     * Alimenta el esquema `DevTenantHeader` de NovaFE (cabecera `X-Tenant-Id`,
     * solo en Development). **Opcional a propósito**: sin esto, las peticiones
     * salen sin identidad y la API responde 401. Ver `identityHeaders()` en
     * `lib/api/server.ts`.
     */
    APP_DEV_TENANT_ID: z.uuid().optional(),
  },

  /**
   * Solo las variables de CLIENTE se listan aquí, y no hay ninguna: las de servidor Next
   * las lee de `process.env` al ejecutar. El objeto vacío es la forma de decir «este
   * frontend no expone nada al navegador», que es justo lo que se quiere — aquí viven las
   * cabeceras de identidad.
   */
  experimental__runtimeEnv: {},

  /**
   * `VAR=` cuenta como no definida. Es lo que hace que `.env.example` se pueda copiar tal
   * cual sin romper el arranque.
   */
  emptyStringAsUndefined: true,

  /**
   * Para construir sin entorno: `SKIP_ENV_VALIDATION=1 bun run build`. Se usa en la imagen
   * de Docker, donde las variables llegan al ejecutar el contenedor y no al construirlo.
   */
  skipValidation: Boolean(process.env.SKIP_ENV_VALIDATION),
});

export const isProduction = process.env.NODE_ENV === "production";
