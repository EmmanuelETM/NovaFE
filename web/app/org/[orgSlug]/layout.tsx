import Link from "next/link";
import { Building } from "lucide-react";

import { NavUser } from "@/components/shared/app-shell";
import { AccessScreen } from "@/features/auth/access-screen";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { ApiError } from "@/lib/api/problem";
import { apiFetch } from "@/lib/api/server";

export const dynamic = "force-dynamic";

const TABS = [
  { href: "", label: "General" },
  { href: "/miembros", label: "Miembros" },
  { href: "/plan", label: "Plan" },
] as const;

/**
 * El shell de una organización (Fase 3) — sin sidebar de tenant: acá no hay
 * un tenant activo, es la organización la que agrupa varios.
 *
 * `params.orgSlug` se resuelve contra `UserProfileDto.organizations` (ya lo
 * trae `/users/me`, sin pegarle de nuevo al backend por id) — no existe un
 * endpoint self-service `GET /organizations/by-slug/{slug}`, y no hace
 * falta: el usuario logueado ya sabe a qué organizaciones pertenece.
 */
export default async function OrganizationLayout({
  children,
  params,
}: LayoutProps<"/org/[orgSlug]">) {
  const { orgSlug } = await params;

  let user: CurrentUser;

  try {
    user = await apiFetch<CurrentUser>("/users/me");
  } catch (error) {
    const esDeLaApi = error instanceof ApiError;

    return (
      <AccessScreen
        status={esDeLaApi ? error.status : 0}
        message={
          esDeLaApi ? error.message : "No se pudo conectar con el servidor."
        }
      />
    );
  }

  const org = user.organizations.find((o) => o.organizationSlug === orgSlug);

  if (!org) {
    return (
      <AccessScreen
        status={403}
        message="No encontramos esa organización, o no tenés acceso."
      />
    );
  }

  return (
    <div className="flex min-h-svh flex-col">
      <header className="flex h-14 shrink-0 items-center gap-3 border-b px-4">
        <div className="bg-muted text-muted-foreground flex size-7 shrink-0 items-center justify-center rounded-lg">
          <Building className="size-4" aria-hidden />
        </div>
        <span className="truncate text-sm font-medium">
          {org.organizationName}
        </span>

        <nav className="ml-6 flex items-center gap-1">
          {TABS.map((tab) => (
            <Link
              key={tab.label}
              href={`/org/${orgSlug}${tab.href}`}
              className="text-muted-foreground hover:text-foreground hover:bg-muted rounded-lg px-3 py-1.5 text-sm transition-colors"
            >
              {tab.label}
            </Link>
          ))}
        </nav>

        <div className="ml-auto">
          <NavUser user={user} />
        </div>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto">{children}</div>
    </div>
  );
}
