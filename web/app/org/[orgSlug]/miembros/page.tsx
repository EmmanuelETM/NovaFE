import type { Metadata } from "next";

import { AccessScreen } from "@/features/auth/access-screen";
import type { CurrentUser } from "@/features/auth/use-current-user";
import { MembersScreen } from "@/features/organizations/members-screen";
import { ApiError } from "@/lib/api/problem";
import { apiFetch } from "@/lib/api/server";

export const metadata: Metadata = {
  title: "Miembros",
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
        message="No encontramos esa organización, o no tenés acceso."
      />
    );
  }

  const canManage = org.role === "owner" || org.role === "admin";

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 p-6">
      <header className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold tracking-tight">Miembros</h1>
        <p className="text-muted-foreground text-sm">
          Quién puede entrar a esta organización y con qué rol.
        </p>
      </header>

      <MembersScreen
        organizationId={org.organizationId}
        canManage={canManage}
      />
    </div>
  );
}
