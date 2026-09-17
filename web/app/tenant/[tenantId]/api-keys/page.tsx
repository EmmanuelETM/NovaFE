import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { ApiKeysScreen } from "@/features/tenant-config/api-keys-screen";
import { tenantHref } from "@/lib/navigation";

export const metadata: Metadata = {
  title: "API Keys",
};

export default async function ApiKeysPage({
  params,
}: PageProps<"/tenant/[tenantId]/api-keys">) {
  const { tenantId } = await params;

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 p-6">
      <PageHeader href={tenantHref(tenantId, "/api-keys")} />
      <ApiKeysScreen />
    </div>
  );
}
