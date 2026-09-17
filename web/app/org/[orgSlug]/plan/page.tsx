import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { Badge } from "@/components/ui/badge";
import { AccessScreen } from "@/features/auth/access-screen";
import { PLAN_OPTIONS } from "@/features/tenants/options";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { ApiError } from "@/lib/api/problem";
import { apiFetch } from "@/lib/api/server";
import { orgHref } from "@/lib/navigation";

export const metadata: Metadata = {
  title: "Plan",
};

function planLabel(plan: string): string {
  return PLAN_OPTIONS.find((option) => option.value === plan)?.label ?? plan;
}

/**
 * Solo lectura por ahora: no hay pasarela de pago todavía, así que no hay
 * nada que cambiar acá — ver `Organization.Plan` en el backend
 * (`docs/multi-tenancy-hierarchy.md`, Fase 2). El plan se administra por
 * ahora del lado del operador.
 */
export default async function PlanPage({
  params,
}: PageProps<"/org/[orgSlug]/plan">) {
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
    <div className="mx-auto flex w-full max-w-2xl flex-col gap-6 p-6">
      <PageHeader href={orgHref(orgSlug, "/plan")} />

      <div className="bg-card border-border/60 flex items-center justify-between rounded-2xl border p-6 shadow-xs">
        <div className="flex flex-col gap-1">
          <span className="text-lg font-semibold">{planLabel(org.plan)}</span>
          <span className="text-muted-foreground text-sm">
            {org.tenants.length} tenant{org.tenants.length === 1 ? "" : "s"}
          </span>
        </div>
        <Badge variant={org.status === "Active" ? "outline" : "secondary"}>
          {org.status === "Active" ? "Activo" : "Suspendido"}
        </Badge>
      </div>

      <p className="text-muted-foreground text-sm">
        ¿Necesitás cambiar de plan? Escribile a tu contacto en Nemus Systems por
        ahora — el autoservicio de facturación todavía no está construido.
      </p>
    </div>
  );
}
