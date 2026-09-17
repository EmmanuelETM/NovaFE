import { NextResponse, type NextRequest } from "next/server";

import type { CurrentUser } from "@/features/auth/use-current-user";
import { apiFetch } from "@/lib/api/server";
import { ApiError } from "@/lib/api/problem";
import {
  IMPERSONATION_COOKIE,
  IMPERSONATION_MAX_AGE_SECONDS,
} from "@/lib/impersonation";

/**
 * Arranca una impersonación (Fase 5): el operador pide ver la API como
 * `userId`. Valida acá **además** de la API (defensa en profundidad, no
 * sustituye la revalidación del lado .NET) porque es quien decide si vale la
 * pena setear la cookie — sin esto, cualquiera con la URL a mano podría
 * intentarlo, aunque el peor caso ya es un 401/403 más adelante.
 *
 * Se invoca como un enlace normal (`<a target="_blank">`), no como una
 * llamada de cliente — por eso es `GET` con querystring en vez de `POST`
 * con body: el único efecto secundario es setear una cookie de sesión, no
 * mutar datos.
 */
export async function GET(request: NextRequest) {
  const userId = request.nextUrl.searchParams.get("userId");
  const email = request.nextUrl.searchParams.get("email");
  const tenantId = request.nextUrl.searchParams.get("tenantId");

  if (!userId || !email || !tenantId) {
    return NextResponse.json(
      { title: "Faltan parámetros para impersonar." },
      { status: 400 },
    );
  }

  let me: CurrentUser;
  try {
    me = await apiFetch<CurrentUser>("/users/me");
  } catch (error) {
    if (error instanceof ApiError) {
      return NextResponse.redirect(new URL("/login", request.url));
    }
    throw error;
  }

  if (me.role !== "admin_sistema") {
    return NextResponse.json(
      { title: "Esta acción es solo para operadores." },
      { status: 403 },
    );
  }

  const response = NextResponse.redirect(
    new URL(`/tenant/${tenantId}`, request.url),
  );

  response.cookies.set(
    IMPERSONATION_COOKIE,
    JSON.stringify({ userId, email }),
    {
      httpOnly: true,
      sameSite: "lax",
      secure: request.nextUrl.protocol === "https:",
      maxAge: IMPERSONATION_MAX_AGE_SECONDS,
      path: "/",
    },
  );

  return response;
}
