import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { OrganizationDetailScreen } from "@/features/organizations/organization-detail-screen";

export const metadata: Metadata = {
  title: "Organización",
};

interface PageProps {
  params: Promise<{ id: string }>;
}

export default async function OrganizationDetailPage({ params }: PageProps) {
  const { id } = await params;

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 p-6">
      <PageHeader href={`/nemus/organizaciones/${id}`} />
      <OrganizationDetailScreen organizationId={id} />
    </div>
  );
}
