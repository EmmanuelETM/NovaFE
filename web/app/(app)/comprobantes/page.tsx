import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { EcfTable } from "@/features/ecf/ecf-table";

export const metadata: Metadata = {
  title: "Comprobantes",
};

export default function ComprobantesPage() {
  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-6 p-6">
      <PageHeader href="/comprobantes" />
      <EcfTable />
    </div>
  );
}
