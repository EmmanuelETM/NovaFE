import Link from "next/link";
import type { Metadata } from "next";
import { Boxes, Building2, ChevronRight } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { AccessScreen } from "@/features/auth/access-screen";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { PLAN_OPTIONS } from "@/features/tenants/options";
import { ApiError } from "@/lib/api/problem";
import { apiFetch } from "@/lib/api/server";

export const metadata: Metadata = {
  title: "Organizaciones",
};

export const dynamic = "force-dynamic";

function planLabel(plan: string): string {
  return PLAN_OPTIONS.find((option) => option.value === plan)?.label ?? plan;
}

/**
 * La vista global: todas las organizaciones del usuario, sin el shell de
 * sidebar/topbar — es un salto de contexto (venís de `OrgSwitcher`), no una
 * pantalla dentro de un tenant o de una organización puntual, así que no
 * calza en ningún `Scope` de `lib/navigation.ts`. Mismo criterio minimalista
 * que `AccessScreen` para lo que vive fuera del shell.
 */
export default async function OrganizacionesPage() {
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

  return (
    <div className="flex min-h-svh flex-col">
      <header className="flex h-14 shrink-0 items-center gap-2.5 border-b px-4">
        <Link href="/" className="flex items-center gap-2.5">
          <div className="bg-sidebar-primary text-sidebar-primary-foreground flex size-7 shrink-0 items-center justify-center rounded-lg">
            <Boxes className="size-4" aria-hidden />
          </div>
          <span className="text-sm font-semibold">NovaFE</span>
        </Link>
      </header>

      <div className="mx-auto flex w-full max-w-3xl flex-1 flex-col gap-6 p-6">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-semibold tracking-tight">
            Organizaciones
          </h1>
          <p className="text-muted-foreground text-sm">
            Todas las organizaciones a las que tienes acceso.
          </p>
        </div>

        {user.organizations.length === 0 ? (
          <div className="border-border/60 text-muted-foreground rounded-2xl border border-dashed p-10 text-center text-sm">
            Todavía no perteneces a ninguna organización.
          </div>
        ) : (
          <div className="flex flex-col gap-2">
            {user.organizations.map((org) => (
              <Link
                key={org.organizationId}
                href={`/org/${org.organizationSlug}`}
                className="bg-card border-border/60 hover:bg-muted/50 flex items-center gap-3 rounded-2xl border p-4 shadow-xs transition-colors"
              >
                <div className="bg-muted text-muted-foreground flex size-9 shrink-0 items-center justify-center rounded-lg">
                  <Building2 className="size-4" aria-hidden />
                </div>
                <div className="flex flex-1 flex-col gap-0.5">
                  <span className="text-sm font-medium">
                    {org.organizationName}
                  </span>
                  <span className="text-muted-foreground text-xs">
                    {org.tenants.length} tenant
                    {org.tenants.length === 1 ? "" : "s"} · tu rol acá:{" "}
                    {org.role}
                  </span>
                </div>
                <Badge variant="outline" className="font-normal">
                  {planLabel(org.plan)}
                </Badge>
                <ChevronRight
                  className="text-muted-foreground size-4"
                  aria-hidden
                />
              </Link>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
