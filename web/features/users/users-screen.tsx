"use client";

import { parseAsString, parseAsStringLiteral, useQueryState } from "nuqs";

import { Button } from "@/components/ui/button";

import { AddUserDialog } from "./add-user-dialog";
import { TenantCombobox } from "./tenant-combobox";
import { UserTable } from "./user-table";
import {
  useOperators,
  useProvisionOperator,
  useProvisionTenantUser,
  useTenantUsers,
} from "./use-users";

const VIEWS = ["operadores", "contribuyente"] as const;

export function UsersScreen() {
  const [view, setView] = useQueryState(
    "vista",
    parseAsStringLiteral(VIEWS).withDefault("operadores"),
  );
  const [tenantId, setTenantId] = useQueryState("tenant", parseAsString);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap gap-2">
        {VIEWS.map((option) => (
          <Button
            key={option}
            size="sm"
            variant={view === option ? "secondary" : "ghost"}
            onClick={() => void setView(option)}
          >
            {option === "operadores"
              ? "Operadores del SaaS"
              : "Usuarios de contribuyente"}
          </Button>
        ))}
      </div>

      {view === "operadores" ? (
        <OperatorsView />
      ) : (
        <TenantUsersView
          tenantId={tenantId}
          onTenantChange={(next) => void setTenantId(next)}
        />
      )}
    </div>
  );
}

function OperatorsView() {
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

function TenantUsersView({
  tenantId,
  onTenantChange,
}: {
  tenantId: string | null;
  onTenantChange: (tenantId: string | null) => void;
}) {
  const { data, isPending, error } = useTenantUsers(tenantId);
  const provision = useProvisionTenantUser();

  return (
    <div className="flex flex-col gap-4">
      <TenantCombobox value={tenantId} onChange={onTenantChange} />

      {tenantId === null ? (
        <p className="text-muted-foreground text-sm">
          Elegí un contribuyente para ver y administrar sus usuarios.
        </p>
      ) : (
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
      )}
    </div>
  );
}
