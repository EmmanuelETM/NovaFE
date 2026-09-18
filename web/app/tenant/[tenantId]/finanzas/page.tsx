import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { FinanceSummaryScreen } from "@/features/finance/finance-summary-screen";
import { tenantHref } from "@/lib/navigation";

export const metadata: Metadata = {
  title: "Finanzas",
};

export default async function FinanzasPage({
  params,
}: PageProps<"/tenant/[tenantId]/finanzas">) {
  const { tenantId } = await params;

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-6 p-6">
      <PageHeader href={tenantHref(tenantId, "/finanzas")} />
      <FinanceSummaryScreen />
    </div>
  );
}
