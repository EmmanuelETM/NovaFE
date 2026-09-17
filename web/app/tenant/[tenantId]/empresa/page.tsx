import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { EmpresaScreen } from "@/features/tenant-config/empresa-screen";
import { tenantHref } from "@/lib/navigation";

export const metadata: Metadata = {
  title: "Empresa",
};

export default async function EmpresaPage({
  params,
}: PageProps<"/tenant/[tenantId]/empresa">) {
  const { tenantId } = await params;

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 p-6">
      <PageHeader href={tenantHref(tenantId, "/empresa")} />
      <EmpresaScreen />
    </div>
  );
}
