import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { EcfDetailScreen } from "@/features/ecf/ecf-detail-screen";
import { tenantHref } from "@/lib/navigation";

export const metadata: Metadata = {
  title: "Comprobante",
};

export default async function ComprobanteDetailPage({
  params,
}: PageProps<"/tenant/[tenantId]/comprobantes/[id]">) {
  const { tenantId, id } = await params;

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 p-6">
      <PageHeader href={tenantHref(tenantId, `/comprobantes/${id}`)} />
      <EcfDetailScreen ecfId={id} />
    </div>
  );
}
