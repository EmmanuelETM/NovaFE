import type { components } from "@/lib/api/schema";

/**
 * Un setting de plataforma, tal como lo devuelve
 * `GET /api/v1/platform-settings` (`PlatformSettingDto`).
 */
export type PlatformSetting = components["schemas"]["PlatformSettingDto"];

/** Cuerpo del `PUT /api/v1/platform-settings/{key}`. */
export type SetSettingValueBody = components["schemas"]["SetSettingValueBody"];
