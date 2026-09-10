/**
 * Un setting de plataforma, tal como lo devuelve
 * `GET /api/v1/platform-settings` (`PlatformSettingDto`).
 *
 * Tipado a mano por ahora — `web/lib/api/schema.d.ts` todavía está vacío. Cuando
 * `bun run api:types` corra (con la API en :5071), reemplazar por:
 *
 *     import type { components } from "@/lib/api/schema";
 *     export type PlatformSetting = components["schemas"]["PlatformSettingDto"];
 *
 * Igual que `web/features/auth/use-current-user.ts` hace con `CurrentUser`.
 */
export interface PlatformSetting {
  key: string;
  group: string;
  label: string;
  description: string | null;
  /** `boolean` | `integer` | `decimal` | `duration` | `string` | `option`. */
  valueType: string;
  unit: string | null;
  /** Restricciones legibles (`"1–100"`, `"letter · pos"`). */
  constraints: string | null;
  /** Opciones válidas (solo `option`); `null` en el resto. */
  options: string[] | null;
  /** Mínimo/máximo ya formateados (numéricos y duración); `null` si no aplica. */
  min: string | null;
  max: string | null;
  killSwitch: boolean;
  sensitive: boolean;
  deprecated: boolean;
  defaultValue: string;
  effectiveValue: string;
  isOverridden: boolean;
  /** `default` o `platform`. */
  resolvedFrom: string;
  updatedAt: string | null;
  updatedBy: string | null;
}

/** Cuerpo del `PUT /api/v1/platform-settings/{key}`. */
export interface SetSettingValueBody {
  value: string;
}
