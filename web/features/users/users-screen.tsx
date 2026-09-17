"use client";

import { AddUserDialog } from "./add-user-dialog";
import { UserTable } from "./user-table";
import { useOperators, useProvisionOperator } from "./use-users";

/**
 * Operadores del SaaS. Los empleados de un contribuyente puntual se
 * gestionan en su propia pestaña "Usuarios" (`/nemus/tenants/[id]`) — vivían
 * acá detrás de un selector de contribuyente, pero es un salto extra sin
 * motivo cuando ya estás mirando ese contribuyente.
 */
export function UsersScreen() {
  const { data, isPending, error } = useOperators();
  const provision = useProvisionOperator();

  return (
    <UserTable
      users={data}
      isPending={isPending}
      error={error}
      tenantId={null}
      emptyTitle="No hay operadores dados de alta."
      toolbarActions={
        <AddUserDialog
          withRole={false}
          label="Agregar operador"
          onCreate={({ email }) => provision.mutateAsync({ email })}
        />
      }
    />
  );
}
