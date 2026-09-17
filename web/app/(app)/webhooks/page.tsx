import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { WebhooksScreen } from "@/features/tenant-config/webhooks-screen";

export const metadata: Metadata = {
  title: "Webhooks",
};

export default function WebhooksPage() {
  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 p-6">
      <PageHeader href="/webhooks" />
      <WebhooksScreen />
    </div>
  );
}
