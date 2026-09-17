import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { TenantOverview } from "@/features/tenant-config/tenant-overview";
import { tenantHref } from "@/lib/navigation";

export const metadata: Metadata = {
  title: "Inicio",
};

/**
 * La pantalla de inicio de un tenant: cuántos e-CF lleva, el último emitido,
 * y el estado del certificado del ambiente activo.
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

      <TenantOverview />
    </div>
  );
}
