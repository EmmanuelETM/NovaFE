/**
 * El contrato de errores de la API, que es uniforme en todos los endpoints.
 *
 * La API devuelve ProblemDetails (RFC 9457) con los mensajes YA en espanol y listos
 * para mostrar. El frontend no traduce ni reescribe: mostrar otra cosa haria que el
 * mensaje de la pantalla y el del log no coincidan, y eso hace imposible depurar por
 * telefono.
 */

/** Forma de un error de la API. */
export interface ProblemDetails {
  type?: string;
  /** Mensaje para el usuario, en espanol. */
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  /** Correlaciona con el log del servidor. Mostrarlo en los errores inesperados. */
  traceId?: string;
  /** Codigo interno del error de dominio, p. ej. `UserProfile.NotRegistered`. */
  code?: string;
  /** Errores por campo. La clave es el nombre del campo del formulario. */
  errors?: Record<string, string[]>;
}

/** Error de la API con su ProblemDetails ya interpretado. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ProblemDetails,
  ) {
    super(problem.title ?? `La peticion fallo con estado ${status}.`);
    this.name = "ApiError";
  }

  /**
   * Errores de validacion por campo, listos para `setError` de react-hook-form.
   *
   * Se toma el primer mensaje de cada campo y se descartan los vacios. Con
   * `noUncheckedIndexedAccess` el compilador exige tratar ese caso, y tiene razon: un
   * campo con arreglo vacio produciria `undefined` donde el formulario espera texto.
   */
  get fieldErrors(): Record<string, string> {
    const result: Record<string, string> = {};

    for (const [field, messages] of Object.entries(this.problem.errors ?? {})) {
      const first = messages.at(0);

      if (first !== undefined) result[field] = first;
    }

    return result;
  }

  /** Si el error es de validacion de campos y no de negocio. */
  get isValidation(): boolean {
    return (
      this.status === 400 && Object.keys(this.problem.errors ?? {}).length > 0
    );
  }

  /** 403 por no estar dado de alta o estar desactivado: no es un error, es falta de acceso. */
  get isAccessDenied(): boolean {
    return this.status === 403;
  }
}

/** Parsea el cuerpo de una respuesta fallida. Nunca lanza. */
export async function readProblem(response: Response): Promise<ProblemDetails> {
  try {
    const cuerpo = (await response.json()) as unknown;

    return typeof cuerpo === "object" && cuerpo !== null
      ? (cuerpo as ProblemDetails)
      : {};
  } catch {
    // 502 de un proxy, o una respuesta vacia. No hay nada que interpretar.
    return {};
  }
}
