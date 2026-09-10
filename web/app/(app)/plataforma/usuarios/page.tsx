import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { UsersScreen } from "@/features/users/users-screen";

export const metadata: Metadata = {
  title: "Usuarios de la plataforma",
};

/**
 * Gestión de accesos humanos (solo operador): operadores del SaaS y, eligiendo un
 * contribuyente, sus empleados. Alta por correo, cambio de rol, revocar y reactivar.
 */
export default function PlataformaUsuariosPage() {
  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 p-6">
      <PageHeader href="/plataforma/usuarios" />
      <UsersScreen />
    </div>
  );
}
