import { createAuthClient } from "better-auth/react";

/**
 * Cliente de Better Auth para componentes de cliente.
 *
 * El sign-in social (`authClient.signIn.social({ provider })`) y `signOut()` son
 * del core — no hace falta ningún plugin de cliente para OAuth.
 */
export const authClient = createAuthClient();
