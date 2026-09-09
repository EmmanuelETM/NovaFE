import { toNextJsHandler } from "better-auth/next-js";

import { auth } from "@/lib/auth";

/**
 * Endpoints de Better Auth: sign-in social, callbacks de OAuth, sesión, sign-out.
 * Todo lo de `/api/auth/*` lo maneja este handler.
 */
export const { GET, POST } = toNextJsHandler(auth);
