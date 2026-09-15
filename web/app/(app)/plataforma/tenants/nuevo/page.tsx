import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { RegisterTenantWizard } from "@/features/tenants/register-tenant-wizard";

export const metadata: Metadata = {
  title: "Nuevo contribuyente",
};

export default function NuevoContribuyentePage() {
  return (
    <div className="mx-auto flex w-full max-w-2xl flex-col gap-6 p-6">
      <PageHeader href="/plataforma/tenants/nuevo" />
      <RegisterTenantWizard />
    </div>
  );
}
