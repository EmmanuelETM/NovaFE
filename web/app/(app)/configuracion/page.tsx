import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { SettingsScreen } from "@/features/tenant-settings/settings-screen";

export const metadata: Metadata = {
  title: "Configuración",
};

/**
 * Configuración self-serve del contribuyente. El catálogo de ajustes vive en el
 * backend (`SettingDefinitions`, scope `Tenant`); esta pantalla solo lista y
 * edita los overrides del propio contribuyente.
 */
export default function ConfiguracionPage() {
  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 p-6">
      <PageHeader href="/configuracion" />
      <SettingsScreen />
    </div>
  );
}
