import { type NextRequest } from "next/server";

import { apiBaseUrl, identityHeaders } from "@/lib/api/server";

/**
 * Proxy hacia la API .NET.
 *
 * Existe para que el navegador hable **siempre con su propio origen**. Eso elimina CORS,
 * permite que la API no sea publica, y mantiene el token de identidad del lado del
 * servidor.
 *
 * Es un paso a traves deliberadamente tonto: no valida ni transforma. La autorizacion la
 * hace la API, que resuelve el rol contra su tabla de usuarios, asi que un navegador
 * curioso solo puede pedir lo que su propia identidad ya permite. Cuando un endpoint
 * necesite logica propia —composicion, agregacion, cache especial— se le escribe su route
 * handler y no se le agrega logica a este.
 */

/**
 * Cuanto se espera a la API antes de rendirse.
 *
 * Holgado a proposito: la consulta mas pesada responde en dos digitos de milisegundos en
 * la red local. Lo que este limite evita es que un fallo de conexion se sienta como
 * lentitud de la aplicacion en lugar de como el error que es.
 */
const REQUEST_TIMEOUT_MS = 10_000;

const METHODS_WITH_BODY = new Set(["POST", "PUT", "PATCH"]);

/** No se reenvian: las pone el fetch o son del transporte del cliente. */
const SKIPPED_HEADERS = new Set([
  "host",
  "connection",
  "content-length",
  "accept-encoding",
  "cookie",
]);

async function forward(
  request: NextRequest,
  context: { params: Promise<{ path: string[] }> },
): Promise<Response> {
  const { path } = await context.params;

  const target = new URL(`${apiBaseUrl()}/${path.join("/")}`);
  target.search = request.nextUrl.search;

  const headers = new Headers();

  request.headers.forEach((value, key) => {
    if (!SKIPPED_HEADERS.has(key.toLowerCase())) headers.set(key, value);
  });

  for (const [key, value] of Object.entries(identityHeaders())) {
    headers.set(key, value);
  }

  let response: Response;

  try {
    response = await fetch(target, {
      method: request.method,
      headers,
      body: METHODS_WITH_BODY.has(request.method)
        ? await request.text()
        : undefined,
      cache: "no-store",
      // No se siguen redirecciones. La API en produccion redirige http a https, y
      // seguir el 307 acabaria en un handshake TLS contra un certificado que este
      // proceso no tiene por que validar. Un redirect aqui es un error de
      // configuracion y conviene verlo como tal.
      redirect: "manual",
      // Un tiempo limite explicito. Sin el, una API colgada se traduce en una espera
      // larguisima que el usuario percibe como "la aplicacion esta lenta" en vez de como
      // un error. Diez segundos es holgado: la consulta mas pesada de la API responde en
      // dos digitos de milisegundos en la red local.
      signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
    });
  } catch (cause) {
    return unreachable(cause);
  }

  if (response.status >= 300 && response.status < 400) {
    return redirected(response.headers.get("location"));
  }

  const passthrough: Record<string, string> = {
    "Content-Type": response.headers.get("Content-Type") ?? "application/json",
  };

  // Sin esta cabecera una descarga llega sin nombre y el navegador la abre en una pestaña
  // en vez de guardarla: el archivo acaba llamandose como la ruta, «export», y el reporte
  // en PDF se muestra como texto. Es la unica cabecera de respuesta que este proxy tiene
  // que reenviar ademas del tipo, y no se nota que falta hasta que hay una descarga.
  const disposition = response.headers.get("Content-Disposition");
  if (disposition) passthrough["Content-Disposition"] = disposition;

  // Util al depurar: dice como quien se actuo. Solo existe en desarrollo.
  const actingAs = response.headers.get("X-Dev-Acting-As");
  if (actingAs) passthrough["X-Dev-Acting-As"] = actingAs;

  // Se devuelve el cuerpo tal cual, incluidos los ProblemDetails: el cliente los
  // interpreta con el mismo codigo sin importar por donde vinieron.
  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers: passthrough,
  });
}

/**
 * La API no responde. Se devuelve un 502 con la misma forma que los errores de la API, en
 * lugar de dejar escapar un `TypeError: fetch failed` como 500 sin cuerpo: el mensaje
 * opaco manda a buscar el problema al frontend, cuando casi siempre es configuracion.
 */
/**
 * La API no responde.
 *
 * El cuerpo es deliberadamente corto y **no menciona configuracion, variables ni puertos**:
 * lo lee quien esta usando el sistema, para quien esos nombres no significan nada, y
 * exponer la forma del despliegue en una respuesta HTTP no aporta y si informa a quien no
 * debe. El detalle tecnico va al log del servidor, que es donde alguien puede actuar sobre
 * el.
 */
function unreachable(cause: unknown): Response {
  const detail = cause instanceof Error ? cause.message : String(cause);

  const code =
    cause instanceof Error && "cause" in cause && cause.cause instanceof Error
      ? (cause.cause as NodeJS.ErrnoException).code
      : undefined;

  const timedOut = cause instanceof Error && cause.name === "TimeoutError";

  const kind = code ?? (cause instanceof Error ? cause.name : "desconocido");

  // Aqui si, con todo el detalle: es para quien mantiene el sistema.
  console.error(`[proxy] no se pudo contactar la API (${kind}): ${detail}`);

  return timedOut
    ? problem({
        status: 504,
        code: "Proxy.ApiTimeout",
        title: "El servidor tardo demasiado en responder.",
      })
    : problem({
        status: 502,
        code: "Proxy.ApiUnreachable",
        title: "No se pudo conectar con el servidor.",
      });
}

/**
 * El destino contesto con una redireccion, que este proxy no sigue. Es un error de
 * configuracion del despliegue, asi que el mensaje al usuario es el mismo de siempre y el
 * dato util queda en el log.
 */
function redirected(location: string | null): Response {
  console.error(
    `[proxy] la API respondio con una redireccion a ${location ?? "(sin Location)"}. ` +
      "Suele ser la redireccion a https activa en desarrollo.",
  );

  return problem({
    status: 502,
    code: "Proxy.ApiUnreachable",
    title: "No se pudo conectar con el servidor.",
  });
}

function problem(body: {
  status: number;
  code: string;
  title: string;
}): Response {
  return new Response(JSON.stringify(body), {
    status: body.status,
    headers: { "Content-Type": "application/problem+json" },
  });
}

export const GET = forward;
export const POST = forward;
export const PUT = forward;
export const PATCH = forward;
export const DELETE = forward;
