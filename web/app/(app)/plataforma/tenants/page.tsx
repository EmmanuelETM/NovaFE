import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { TenantTable } from "@/features/tenants/tenant-table";

export const metadata: Metadata = {
  title: "Contribuyentes",
};

/**
 * Alta y gestión de contribuyentes (solo operador): el listado enlaza al
 * detalle de cada uno, donde viven las pestañas de perfil, certificados,
 * secuencias y API keys. El alta guiada vive en `/plataforma/tenants/nuevo`.
 */
export default function PlataformaTenantsPage() {
  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-6 p-6">
      <PageHeader href="/plataforma/tenants" />
      <TenantTable />
    </div>
  );
}
