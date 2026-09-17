import "server-only";

/**
 * Impersonación de solo lectura (Fase 5): un operador (`admin_sistema`) pide
 * ver la API como otro usuario, para soporte. La cookie es la única fuente
 * de verdad del lado del dashboard — `identityHeaders()` la traduce a
 * `X-Impersonate-User-Id` en cada petición mientras dure, y el banner la lee
 * para mostrarse. Ver `docs/human-auth.md` (lado .NET) y
 * `app/api/impersonate/route.ts`.
 */
export const IMPERSONATION_COOKIE = "impersonate";

/** 30 minutos: alcanza para una sesión de soporte, corta si se olvida cerrarla. */
export const IMPERSONATION_MAX_AGE_SECONDS = 30 * 60;

export interface ImpersonationState {
  userId: string;
  email: string;
}

/**
 * Lee y valida el valor de la cookie. `null` si no existe o no tiene la
 * forma esperada — nunca confiar en JSON crudo sin validar, aunque lo haya
 * puesto este mismo servidor.
 */
export function parseImpersonationCookie(
  raw: string | undefined,
): ImpersonationState | null {
  if (!raw) return null;

  try {
    const parsed: unknown = JSON.parse(raw);

    if (
      typeof parsed === "object" &&
      parsed !== null &&
      typeof (parsed as Record<string, unknown>).userId === "string" &&
      typeof (parsed as Record<string, unknown>).email === "string"
    ) {
      return parsed as ImpersonationState;
    }

    return null;
  } catch {
    return null;
  }
}
