import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { SequencesScreen } from "@/features/tenant-config/sequences-screen";

export const metadata: Metadata = {
  title: "Secuencias",
};

export default function SecuenciasPage() {
  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 p-6">
      <PageHeader href="/secuencias" />
      <SequencesScreen />
    </div>
  );
}
