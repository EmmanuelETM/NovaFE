import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { CertificatesScreen } from "@/features/tenant-config/certificates-screen";
import { tenantHref } from "@/lib/navigation";

export const metadata: Metadata = {
  title: "Certificados",
};

export default async function CertificadosPage({
  params,
}: PageProps<"/tenant/[tenantId]/certificados">) {
  const { tenantId } = await params;

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 p-6">
      <PageHeader href={tenantHref(tenantId, "/certificados")} />
      <CertificatesScreen />
    </div>
  );
}
