import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { AuditLogScreen } from "@/features/tenant-config/audit-log-screen";
import { tenantHref } from "@/lib/navigation";

export const metadata: Metadata = {
  title: "Logs de auditoría",
};

export default async function AuditoriaPage({
  params,
}: PageProps<"/tenant/[tenantId]/auditoria">) {
  const { tenantId } = await params;

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 p-6">
      <PageHeader href={tenantHref(tenantId, "/auditoria")} />
      <AuditLogScreen />
    </div>
  );
}
