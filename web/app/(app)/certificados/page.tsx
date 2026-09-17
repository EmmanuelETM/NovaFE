import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { CertificatesScreen } from "@/features/tenant-config/certificates-screen";

export const metadata: Metadata = {
  title: "Certificados",
};

export default function CertificadosPage() {
  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 p-6">
      <PageHeader href="/certificados" />
      <CertificatesScreen />
    </div>
  );
}
