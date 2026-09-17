"use client";

import { AddUserDialog } from "@/features/users/add-user-dialog";
import { UserTable } from "@/features/users/user-table";
import {
  useProvisionTenantUser,
  useTenantUsers,
} from "@/features/users/use-users";

/** Los usuarios (empleados) de un contribuyente puntual, vistos por el operador. */
export function UsersTab({ tenantId }: { tenantId: string }) {
  const { data, isPending, error } = useTenantUsers(tenantId);
  const provision = useProvisionTenantUser();

  return (
    <UserTable
      users={data}
      isPending={isPending}
      error={error}
      tenantId={tenantId}
      emptyTitle="Este contribuyente no tiene usuarios."
      toolbarActions={
        <AddUserDialog
          withRole
          label="Agregar usuario"
          onCreate={({ email, role }) =>
            provision.mutateAsync({ tenantId, email, role })
          }
        />
      }
    />
  );
}
