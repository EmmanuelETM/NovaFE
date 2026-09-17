import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { EmpresaScreen } from "@/features/tenant-config/empresa-screen";

export const metadata: Metadata = {
  title: "Empresa",
};

export default function EmpresaPage() {
  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 p-6">
      <PageHeader href="/empresa" />
      <EmpresaScreen />
    </div>
  );
}
