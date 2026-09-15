import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { TenantDetailScreen } from "@/features/tenants/tenant-detail-screen";

export const metadata: Metadata = {
  title: "Contribuyente",
};

interface PageProps {
  params: Promise<{ id: string }>;
}

export default async function TenantDetailPage({ params }: PageProps) {
  const { id } = await params;

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 p-6">
      <PageHeader href={`/plataforma/tenants/${id}`} />
      <TenantDetailScreen tenantId={id} />
    </div>
  );
}
