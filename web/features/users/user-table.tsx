"use client";

import type { ReactNode } from "react";

import {
  DataTable,
  STATIC_TABLE_STATE,
  createAppColumnHelper,
  toStaticPage,
} from "@/components/shared/data-table";
import { Badge } from "@/components/ui/badge";
import { roleLabel } from "@/features/auth/roles";
import { formatDate } from "@/lib/format";

import { isActive, type PlatformUser } from "./types";
import { UserActions } from "./user-actions";

const ch = createAppColumnHelper<PlatformUser>();

function columnsFor(tenantId: string | null) {
  return ch.columns([
    ch.accessor("email", { header: "Correo", meta: { label: "Correo" } }),
    ch.accessor("role", {
      header: "Rol",
      cell: (cell) => (
        <Badge variant="secondary">{roleLabel(cell.getValue())}</Badge>
      ),
    }),
    ch.display({
      id: "estado",
      header: "Estado",
      cell: (cell) =>
        isActive(cell.row.original) ? (
          <Badge variant="outline">Activo</Badge>
        ) : (
          <Badge variant="destructive">Revocado</Badge>
        ),
    }),
    ch.accessor("authLinked", {
      header: "Sesión",
      cell: (cell) => (cell.getValue() ? "Enlazada" : "Pendiente"),
    }),
    ch.accessor("createdAt", {
      header: "Alta",
      cell: (cell) => formatDate(cell.getValue()),
    }),
    ch.display({
      id: "acciones",
      header: "",
      meta: { align: "right" },
      cell: (cell) => (
        <UserActions user={cell.row.original} tenantId={tenantId} />
      ),
    }),
  ]);
}

interface UserTableProps {
  users: PlatformUser[] | undefined;
  isPending: boolean;
  error?: unknown;
  tenantId: string | null;
  emptyTitle: string;
  toolbarActions?: ReactNode;
}

export function UserTable({
  users,
  isPending,
  error,
  tenantId,
  emptyTitle,
  toolbarActions,
}: UserTableProps) {
  return (
    <DataTable
      columns={columnsFor(tenantId)}
      page={toStaticPage(users)}
      isPending={isPending}
      error={error}
      state={STATIC_TABLE_STATE}
      onStateChange={() => {}}
      searchable={false}
      emptyState={{ title: emptyTitle }}
      toolbarActions={toolbarActions}
      getRowId={(user) => user.id}
    />
  );
}
