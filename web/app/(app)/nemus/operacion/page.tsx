import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { OpsLiveIndicator } from "@/features/ops/live-indicator";
import { OpsStatusScreen } from "@/features/ops/ops-status-screen";

export const metadata: Metadata = {
  title: "Operación de la plataforma",
};

/**
 * Estado operacional en vivo (solo operador): latido de los workers de
 * fondo, profundidad y antigüedad de los outbox (DGII + webhooks), y qué
 * tenants tienen una secuencia e-NCF por agotarse. Ver docs/observability.md
 * en el backend.
 */
export default function NemusOperacionPage() {
  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 p-6">
      <PageHeader href="/nemus/operacion" actions={<OpsLiveIndicator />} />
      <OpsStatusScreen />
    </div>
  );
}
