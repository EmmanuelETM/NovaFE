import type { components } from "@/lib/api/schema";

/**
 * Un setting que el contribuyente puede ajustar, tal como lo devuelve
 * `GET /api/v1/settings` (`TenantSettingDto`).
 */
export type TenantSetting = components["schemas"]["TenantSettingDto"];

/** Una entrada del historial de un setting (`TenantSettingChangeDto`). */
export type TenantSettingChange =
  components["schemas"]["TenantSettingChangeDto"];
