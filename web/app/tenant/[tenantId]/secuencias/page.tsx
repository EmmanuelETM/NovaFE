import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { SequencesScreen } from "@/features/tenant-config/sequences-screen";
import { tenantHref } from "@/lib/navigation";

export const metadata: Metadata = {
  title: "Secuencias",
};

export default async function SecuenciasPage({
  params,
}: PageProps<"/tenant/[tenantId]/secuencias">) {
  const { tenantId } = await params;

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 p-6">
      <PageHeader href={tenantHref(tenantId, "/secuencias")} />
      <SequencesScreen />
    </div>
  );
}
