import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { OrganizationTable } from "@/features/organizations/organization-table";

export const metadata: Metadata = {
  title: "Organizaciones",
};

/**
 * Las organizaciones de la plataforma (solo operador): la cuenta pagadora
 * que agrupa uno o más tenants. El listado enlaza al detalle de cada una,
 * donde viven el plan, el estado y sus miembros/tenants.
 */
export default function NemusOrganizacionesPage() {
  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-6 p-6">
      <PageHeader href="/nemus/organizaciones" />
      <OrganizationTable />
    </div>
  );
}
