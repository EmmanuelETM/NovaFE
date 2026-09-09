import { getSessionCookie } from "better-auth/cookies";
import { NextResponse, type NextRequest } from "next/server";

/**
 * Puerta al dashboard.
 *
 * **No valida la sesión** — solo mira si existe la cookie de Better Auth, que es
 * una lectura sin I/O y anda en el runtime edge. La validación real la hacen el
 * layout de servidor (`GET /users/me`) y la API en cada petición. Esto solo evita
 * pintar el shell para redirigir un instante después.
 *
 * En desarrollo, `APP_DEV_TENANT_ID` habilita el atajo `X-Tenant-Id` de NovaFE:
 * ahí se entra sin sesión.
 */
export function proxy(request: NextRequest) {
  const hasSession = getSessionCookie(request) !== null;
  const devBypass = Boolean(process.env.APP_DEV_TENANT_ID);

  if (hasSession || devBypass) {
    return NextResponse.next();
  }

  const login = new URL("/login", request.url);
  login.searchParams.set(
    "next",
    request.nextUrl.pathname + request.nextUrl.search,
  );
  return NextResponse.redirect(login);
}

export const config = {
  /*
   * Todo menos:
   * - `login` / `auth-error` — las pantallas de acceso;
   * - `api/auth/*` — los endpoints de Better Auth;
   * - `api/backend/*` — el proxy a la API (hace su propia auth; devolver HTML acá
   *   rompería los `fetch` del cliente);
   * - los assets de Next.
   */
  matcher: [
    "/((?!login|auth-error|api/auth|api/backend|_next/static|_next/image|favicon.ico).*)",
  ],
};
