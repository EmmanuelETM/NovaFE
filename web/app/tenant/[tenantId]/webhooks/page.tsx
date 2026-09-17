import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { WebhooksScreen } from "@/features/tenant-config/webhooks-screen";
import { tenantHref } from "@/lib/navigation";

export const metadata: Metadata = {
  title: "Webhooks",
};

export default async function WebhooksPage({
  params,
}: PageProps<"/tenant/[tenantId]/webhooks">) {
  const { tenantId } = await params;

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 p-6">
      <PageHeader href={tenantHref(tenantId, "/webhooks")} />
      <WebhooksScreen />
    </div>
  );
}
