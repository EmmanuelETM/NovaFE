import { NextResponse, type NextRequest } from "next/server";

import { IMPERSONATION_COOKIE } from "@/lib/impersonation";

/** Termina la impersonación: borra la cookie y vuelve a la consola de operador. */
export function GET(request: NextRequest) {
  const response = NextResponse.redirect(
    new URL("/nemus/usuarios", request.url),
  );

  response.cookies.delete(IMPERSONATION_COOKIE);

  return response;
}
