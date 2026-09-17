import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { EcfDetailScreen } from "@/features/ecf/ecf-detail-screen";

export const metadata: Metadata = {
  title: "Comprobante",
};

interface PageProps {
  params: Promise<{ id: string }>;
}

export default async function ComprobanteDetailPage({ params }: PageProps) {
  const { id } = await params;

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 p-6">
      <PageHeader href={`/comprobantes/${id}`} />
      <EcfDetailScreen ecfId={id} />
    </div>
  );
}
