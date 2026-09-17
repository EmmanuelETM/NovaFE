import { ApiError, readProblem } from "./problem";

/**
 * Cliente de la API para **componentes de cliente**.
 *
 * Apunta al proxy del mismo origen, no a la API .NET. Por eso no lleva cabeceras de
 * identidad: eso lo pone el servidor.
 *
 * Se usa desde los `queryFn` y `mutationFn` de TanStack Query. Los errores salen como
 * `ApiError`, asi que un solo manejador cubre validacion y negocio.
 */

const BASE = "/api/backend";

type Query = Record<string, string | number | boolean | undefined | null>;

function withQuery(path: string, query?: Query): string {
  const search = new URLSearchParams();

  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== undefined && value !== null && value !== "") {
      search.set(key, String(value));
    }
  }

  const clean = path.startsWith("/") ? path : `/${path}`;
  const qs = search.toString();

  return `${BASE}${clean}${qs ? `?${qs}` : ""}`;
}

/**
 * `X-Active-Tenant-Id` es una **pista**, no una credencial: el proxy
 * (`app/api/backend/[...path]/route.ts`) la lee y se la pasa a
 * `identityHeaders()` del lado servidor, que recien ahi arma el
 * `X-Acting-Tenant-Id` de verdad. Un cliente no puede colarse a otro tenant
 * mandando cualquier id — el backend igual exige acceso real.
 */
function withTenant(
  headers: HeadersInit | undefined,
  tenantId?: string,
): HeadersInit {
  return tenantId
    ? { ...headers, "X-Active-Tenant-Id": tenantId }
    : (headers ?? {});
}

async function request<T>(
  path: string,
  init: RequestInit,
  query?: Query,
  tenantId?: string,
): Promise<T> {
  const isForm = init.body instanceof FormData;

  const response = await fetch(withQuery(path, query), {
    ...init,
    headers: withTenant(
      isForm
        ? init.headers
        : { "Content-Type": "application/json", ...init.headers },
      tenantId,
    ),
  });

  if (!response.ok)
    throw new ApiError(response.status, await readProblem(response));

  if (response.status === 204) return undefined as T;

  return (await response.json()) as T;
}

export const api = {
  get: <T>(path: string, query?: Query, tenantId?: string) =>
    request<T>(path, { method: "GET" }, query, tenantId),

  post: <T>(path: string, body?: unknown, tenantId?: string) =>
    request<T>(
      path,
      { method: "POST", body: JSON.stringify(body ?? {}) },
      undefined,
      tenantId,
    ),

  put: <T>(path: string, body?: unknown, tenantId?: string) =>
    request<T>(
      path,
      { method: "PUT", body: JSON.stringify(body ?? {}) },
      undefined,
      tenantId,
    ),

  /**
   * Igual que `post`, pero para `multipart/form-data` (subir un archivo). No
   * fija `Content-Type`: el navegador le agrega el `boundary` solo cuando el
   * cuerpo es un `FormData` y no hay esa cabecera puesta (ver `isForm` en
   * `request`).
   */
  postForm: <T>(path: string, form: FormData, tenantId?: string) =>
    request<T>(path, { method: "POST", body: form }, undefined, tenantId),

  patch: <T>(path: string, body?: unknown, tenantId?: string) =>
    request<T>(
      path,
      { method: "PATCH", body: JSON.stringify(body ?? {}) },
      undefined,
      tenantId,
    ),

  delete: <T>(path: string, tenantId?: string) =>
    request<T>(path, { method: "DELETE" }, undefined, tenantId),

  /**
   * Baja un archivo que genera la API (un reporte en CSV o en PDF).
   *
   * No devuelve datos: **provoca la descarga**. El nombre del archivo lo decide la API y
   * viaja en `Content-Disposition`, asi que la pantalla no lo inventa — el mismo reporte
   * bajado desde dos sitios distintos se llama igual.
   *
   * Va por `fetch` y un enlace temporal, y no por `window.open`, por dos razones: un error
   * sale como `ApiError` y se puede mostrar como cualquier otro, en vez de abrir una
   * pestana con un ProblemDetails en crudo; y una pestana emergente la bloquea el
   * navegador cuando la descarga tarda.
   */
  download: async (
    path: string,
    query?: Query,
    tenantId?: string,
  ): Promise<void> => {
    const response = await fetch(withQuery(path, query), {
      method: "GET",
      headers: withTenant(undefined, tenantId),
    });

    if (!response.ok)
      throw new ApiError(response.status, await readProblem(response));

    const blob = await response.blob();
    const url = URL.createObjectURL(blob);

    const link = document.createElement("a");
    link.href = url;
    link.download = fileNameFrom(response.headers.get("Content-Disposition"));
    document.body.append(link);
    link.click();
    link.remove();

    // Sin esto el blob se queda en memoria hasta que se cierre la pestana, y un reporte
    // en PDF no es pequeno.
    URL.revokeObjectURL(url);
  },
};

/**
 * El nombre del archivo, sacado de `Content-Disposition`.
 *
 * Se prefiere `filename*`, que es el que viene codificado en UTF-8: el otro pierde las
 * tildes, y estos nombres las llevan. Si la cabecera no llega —o el proxy no la
 * reenviara— queda un nombre generico, que es mejor que una descarga sin nombre.
 */
function fileNameFrom(disposition: string | null): string {
  if (!disposition) return "reporte";

  const utf8 = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  if (utf8?.[1]) return decodeURIComponent(utf8[1]);

  const simple = /filename="?([^";]+)"?/i.exec(disposition);

  return simple?.[1] ?? "reporte";
}
