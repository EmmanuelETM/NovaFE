import Link from "next/link";
import type { Metadata } from "next";
import { Building, ChevronRight } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { AccessScreen } from "@/features/auth/access-screen";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { ApiError } from "@/lib/api/problem";
import { apiFetch } from "@/lib/api/server";

export const metadata: Metadata = {
  title: "Organización",
};

export default async function OrganizationHomePage({
  params,
}: PageProps<"/org/[orgSlug]">) {
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
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 p-6">
      <header className="flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">
          {org.organizationName}
        </h1>
        <Badge variant="outline">{org.plan}</Badge>
        <Badge variant={org.status === "Active" ? "outline" : "secondary"}>
          {org.status === "Active" ? "Activa" : "Suspendida"}
        </Badge>
      </header>

      <div className="flex flex-col gap-2">
        <h2 className="text-muted-foreground text-sm font-medium">
          Tenants ({org.tenants.length})
        </h2>

        {org.tenants.length === 0 ? (
          <div className="bg-card text-muted-foreground rounded-2xl border p-6 text-sm">
            Todavía no hay ningún tenant asociado a esta organización.
          </div>
        ) : (
          <div className="flex flex-col gap-2">
            {org.tenants.map((tenant) => (
              <Link
                key={tenant.tenantId}
                href={`/tenant/${tenant.tenantId}`}
                className="bg-card hover:bg-muted/50 flex items-center gap-3 rounded-2xl border p-4 transition-colors"
              >
                <div className="bg-muted text-muted-foreground flex size-9 shrink-0 items-center justify-center rounded-lg">
                  <Building className="size-4" aria-hidden />
                </div>
                <div className="flex flex-1 flex-col">
                  <span className="text-sm font-medium">
                    {tenant.tenantName}
                  </span>
                  <span className="text-muted-foreground text-xs">
                    Tu rol acá: {tenant.role}
                  </span>
                </div>
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
