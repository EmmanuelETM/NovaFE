import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { StatTile } from "@/components/shared/stat-tile";
import { tenantHref } from "@/lib/navigation";

export const metadata: Metadata = {
  title: "Inicio",
};

/**
 * La pantalla de inicio de un tenant.
 *
 * Vive dentro de `tenant/[tenantId]` para heredar el sidebar y la barra superior con ese
 * tenant activo; fuera de esa carpeta no los tendría. El título y la descripción no se
 * escriben aquí: `PageHeader` los saca de `lib/navigation.ts`, la misma definición que
 * pinta el enlace del sidebar.
 */
export default async function InicioPage({
  params,
}: PageProps<"/tenant/[tenantId]">) {
  const { tenantId } = await params;

  return (
    <div className="mx-auto flex w-full max-w-7xl flex-col gap-6 p-6">
      <PageHeader href={tenantHref(tenantId, "/")} />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile label="Una cifra" value="1,234" hint="con su contexto" />
        <StatTile label="Otra cifra" value="RD$8,900.00" />
        <StatTile label="Algo que atender" value="3" highlight />
        <StatTile label="Cargando" value="" loading />
      </div>

      <div className="bg-card text-muted-foreground rounded-2xl border p-6 text-sm">
        <p>
          Este es el scaffold. Agrega tus pantallas en{" "}
          <code className="font-mono">app/tenant/[tenantId]/</code>, su entrada
          en <code className="font-mono">lib/navigation.ts</code>, y la lógica
          de cada módulo en <code className="font-mono">features/</code>.
        </p>
      </div>
    </div>
  );
}
