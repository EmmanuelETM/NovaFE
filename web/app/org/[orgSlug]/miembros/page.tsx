import type { Metadata } from "next";

import { PageHeader } from "@/components/shared/app-shell";
import { AccessScreen } from "@/features/auth/access-screen";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { MembersScreen } from "@/features/organizations/members-screen";
import { ApiError } from "@/lib/api/problem";
import { apiFetch } from "@/lib/api/server";
import { orgHref } from "@/lib/navigation";

export const metadata: Metadata = {
  title: "Equipo",
};

export default async function MiembrosPage({
  params,
}: PageProps<"/org/[orgSlug]/miembros">) {
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
        message="No encontramos esa organización, o no tienes acceso."
      />
    );
  }

  const canManage = org.role === "owner" || org.role === "admin";

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 p-6">
      <PageHeader href={orgHref(orgSlug, "/miembros")} />

      <MembersScreen
        organizationId={org.organizationId}
        canManage={canManage}
      />
    </div>
  );
}
