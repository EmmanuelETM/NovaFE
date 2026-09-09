import "server-only";

import { getSession } from "@/lib/auth/session";
import { env } from "@/lib/env";

import { ApiError, readProblem } from "./problem";

/**
 * Cliente de la API .NET, **solo del lado del servidor**.
 *
 * Es la unica costura de identidad del proyecto: aqui se decide como se autentica, y solo
 * aqui.
 *
 * - Con sesion de Better Auth: el BFF actua en nombre del humano contra la API — manda
 *   `X-Internal-Key` (secreto compartido) + `X-Acting-User`/`X-Acting-Email`. La API
 *   resuelve un `PlatformUser` y emite los mismos claims que una API key.
 * - Sin sesion, en desarrollo: la cabecera `X-Tenant-Id` con el contribuyente de
 *   `.env.local` (esquema `DevTenantHeader` de NovaFE, solo en Development).
 * - Sin nada: la API responde 401 y el layout muestra la pantalla de acceso.
 *
 * **El navegador nunca llama a la API .NET directamente.** Pasa por el proxy de
 * `app/api/backend/[...path]`. De ahi salen tres cosas: no hace falta CORS, la API puede
 * no ser publica, y las cabeceras de identidad nunca llegan al navegador.
 */

interface RequestOptions extends Omit<RequestInit, "body"> {
  /** Se serializa a JSON. Omitir en GET. */
  body?: unknown;
  /** Parametros de consulta. Los vacios se omiten. */
  query?: Record<string, string | number | boolean | undefined | null>;
}

/**
 * Identidad de la peticion. Ver la doc del modulo.
 *
 * Es `async` porque lee la sesion de Better Auth. **Cambiar esta funcion es lo unico que
 * hace falta** si cambia el proveedor de identidad.
 */
export async function identityHeaders(): Promise<Record<string, string>> {
  const session = await getSession();

  if (session?.user && env.INTERNAL_API_KEY) {
    return {
      "X-Internal-Key": env.INTERNAL_API_KEY,
      "X-Acting-User": session.user.id,
      "X-Acting-Email": session.user.email,
    };
  }

  if (env.APP_DEV_TENANT_ID) return { "X-Tenant-Id": env.APP_DEV_TENANT_ID };

  return {};
}

/** Base de la API, incluida la version. */
export function apiBaseUrl(): string {
  return `${env.APP_API_URL}/api/${env.APP_API_VERSION}`;
}

function buildUrl(path: string, query?: RequestOptions["query"]): string {
  const clean = path.startsWith("/") ? path : `/${path}`;
  const url = new URL(`${apiBaseUrl()}${clean}`);

  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== undefined && value !== null && value !== "") {
      url.searchParams.set(key, String(value));
    }
  }

  return url.toString();
}

/**
 * Llama a la API y devuelve el cuerpo tipado.
 *
 * @throws {ApiError} Con el ProblemDetails ya interpretado si la respuesta no es 2xx.
 */
export async function apiFetch<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  const { body, query, headers, ...rest } = options;

  const response = await fetch(buildUrl(path, query), {
    ...rest,
    headers: {
      "Content-Type": "application/json",
      ...(await identityHeaders()),
      ...headers,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    // Las pantallas deciden su cache con TanStack Query; Next no debe cachear por encima
    // o un cambio de precio tardaria en verse sin que nadie sepa por que.
    cache: "no-store",
  });

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response));
  }

  // 204 en los PATCH y DELETE.
  if (response.status === 204) return undefined as T;

  return (await response.json()) as T;
}
