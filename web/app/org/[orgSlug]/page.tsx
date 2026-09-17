import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { AccessScreen } from "@/features/auth/access-screen";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { TenantsGrid } from "@/features/organizations/tenants-grid";
import { ApiError } from "@/lib/api/problem";
import { apiFetch } from "@/lib/api/server";
import { orgHref } from "@/lib/navigation";

export const metadata: Metadata = {
  title: "Tenants",
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
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 p-6">
      <PageHeader href={orgHref(orgSlug, "/")} />
      <TenantsGrid organizationId={org.organizationId} />
    </div>
  );
}
