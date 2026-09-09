import "server-only";

import { env } from "@/lib/env";

import type { SocialProvider } from "./social";

export type { SocialProvider } from "./social";
export { SOCIAL_PROVIDERS, SOCIAL_PROVIDER_LABELS } from "./social";

/**
 * Los providers de OAuth con credenciales en el entorno. Mismo criterio que
 * `socialProviders` en `./index`. El login pinta un botón por cada uno; los que
 * faltan se habilitan poniendo sus `*_CLIENT_ID` / `*_CLIENT_SECRET`.
 */
export function enabledSocialProviders(): SocialProvider[] {
  const enabled: SocialProvider[] = [];
  if (env.GITHUB_CLIENT_ID && env.GITHUB_CLIENT_SECRET) enabled.push("github");
  if (env.GOOGLE_CLIENT_ID && env.GOOGLE_CLIENT_SECRET) enabled.push("google");
  if (env.MICROSOFT_CLIENT_ID && env.MICROSOFT_CLIENT_SECRET)
    enabled.push("microsoft");
  return enabled;
}
