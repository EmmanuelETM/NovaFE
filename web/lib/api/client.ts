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

async function request<T>(
  path: string,
  init: RequestInit,
  query?: Query,
): Promise<T> {
  const response = await fetch(withQuery(path, query), {
    ...init,
    headers: { "Content-Type": "application/json", ...init.headers },
  });

  if (!response.ok)
    throw new ApiError(response.status, await readProblem(response));

  if (response.status === 204) return undefined as T;

  return (await response.json()) as T;
}

export const api = {
  get: <T>(path: string, query?: Query) =>
    request<T>(path, { method: "GET" }, query),

  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "POST", body: JSON.stringify(body ?? {}) }),

  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "PUT", body: JSON.stringify(body ?? {}) }),

  patch: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: "PATCH", body: JSON.stringify(body ?? {}) }),

  delete: <T>(path: string) => request<T>(path, { method: "DELETE" }),

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
  download: async (path: string, query?: Query): Promise<void> => {
    const response = await fetch(withQuery(path, query), { method: "GET" });

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
