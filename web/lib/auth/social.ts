/**
 * Metadatos de los providers de OAuth — **client-safe** (sin `server-only`, sin
 * `env`). El cálculo de cuáles están habilitados vive en `./providers` (servidor).
 */
export const SOCIAL_PROVIDERS = ["github", "google", "microsoft"] as const;

export type SocialProvider = (typeof SOCIAL_PROVIDERS)[number];

export const SOCIAL_PROVIDER_LABELS: Record<SocialProvider, string> = {
  github: "GitHub",
  google: "Google",
  microsoft: "Microsoft",
};
