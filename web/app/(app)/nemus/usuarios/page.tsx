import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { UsersScreen } from "@/features/users/users-screen";

export const metadata: Metadata = {
  title: "Usuarios de la plataforma",
};

/**
 * Gestión de accesos humanos (solo operador): los operadores del SaaS. Los
 * empleados de un contribuyente puntual se gestionan en su propia pestaña
 * "Usuarios", dentro de `/nemus/tenants/[id]`.
 */
export default function NemusUsuariosPage() {
  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 p-6">
      <PageHeader href="/nemus/usuarios" />
      <UsersScreen />
    </div>
  );
}
