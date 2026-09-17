import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { EcfTable } from "@/features/ecf/ecf-table";
import { tenantHref } from "@/lib/navigation";

export const metadata: Metadata = {
  title: "Comprobantes",
};

export default async function ComprobantesPage({
  params,
}: PageProps<"/tenant/[tenantId]/comprobantes">) {
  const { tenantId } = await params;

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-6 p-6">
      <PageHeader href={tenantHref(tenantId, "/comprobantes")} />
      <EcfTable />
    </div>
  );
}
