import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { SettingsScreen } from "@/features/settings/settings-screen";

export const metadata: Metadata = {
  title: "Configuración de la plataforma",
};

/**
 * Configuración operativa de la plataforma (solo operador). El catálogo de
 * ajustes vive en el backend (`SettingDefinitions`); esta pantalla solo lista y
 * edita los overrides.
 */
export default function PlataformaConfiguracionPage() {
  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 p-6">
      <PageHeader href="/plataforma/configuracion" />
      <SettingsScreen />
    </div>
  );
}
