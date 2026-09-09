import type { ReactNode } from "react";

/**
 * Las pantallas de acceso (login, error de OAuth). Sin el shell de la app: no hay
 * sesión todavía, así que no hay sidebar ni navegación por rol.
 */
export default function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-svh flex-col items-center justify-center gap-6 p-6">
      {children}
    </div>
  );
}
