"use client";

import { formatRelative } from "./format";
import { useNow } from "./use-now";
import { useOpsStatus } from "./use-ops-status";

/**
 * El punto que titila + «actualizado hace Xs», para el `actions` del
 * `PageHeader`. Reusa la misma query que la pantalla (misma `queryKey`, mismo
 * `QueryClient`): no dispara una petición aparte, solo lee la caché.
 */
export function OpsLiveIndicator() {
  const { data } = useOpsStatus();
  const now = useNow();

  return (
    <div className="text-muted-foreground flex items-center gap-2 text-xs">
      <span className="relative flex size-2">
        <span className="bg-foreground/40 absolute inline-flex h-full w-full animate-ping rounded-full" />
        <span className="bg-foreground/70 relative inline-flex size-2 rounded-full" />
      </span>
      {data
        ? `Actualizado ${formatRelative(new Date(data.generatedAt).getTime(), now)}`
        : "Cargando…"}
    </div>
  );
}
