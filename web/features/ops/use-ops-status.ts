"use client";

import { useQuery } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

import type { OpsStatus } from "./types";

/**
 * Estado operacional en vivo. A diferencia del resto de las pantallas —donde
 * los datos cambian por lo que hace la propia persona—, este es un mostrador:
 * se refresca solo cada 15 s, sin que nadie tenga que recargar la página.
 */
export function useOpsStatus() {
  return useQuery({
    queryKey: queryKeys.ops.status(),
    queryFn: () => api.get<OpsStatus>("/ops/status"),
    refetchInterval: 15_000,
  });
}
