"use client";

import { useQuery } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

import type { PlatformSettingChange } from "./types";

/** El historial de cambios de un setting. `enabled` para no pedirlo hasta abrir el dialog. */
export function usePlatformSettingHistory(key: string, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.platformSettings.history(key),
    queryFn: () =>
      api.get<PlatformSettingChange[]>(
        `/platform-settings/${encodeURIComponent(key)}/history`,
      ),
    enabled,
  });
}
