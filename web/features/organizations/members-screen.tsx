"use client";

import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useController, useForm } from "react-hook-form";
import { z } from "zod";
import { MoreHorizontal, Plus } from "lucide-react";

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  DataTable,
  STATIC_TABLE_STATE,
  createAppColumnHelper,
  toStaticPage,
} from "@/components/shared/data-table";
import { applyFieldErrors } from "@/lib/api/form-errors";
import { selectItems } from "@/lib/select-items";
import { formatDate } from "@/lib/format";

import { ORGANIZATION_ROLE_OPTIONS, organizationRoleLabel } from "./types";
import type { OrganizationMember } from "./types";
import {
  organizationErrorMessage,
  useAddOrganizationMember,
  useChangeOrganizationMemberRole,
  useOrganizationMembers,
  useRemoveOrganizationMember,
} from "./use-organizations";

interface MembersScreenProps {
  organizationId: string;
  /** Si el usuario actual es `owner`/`admin` de **esta** organización. */
  canManage: boolean;
}

const ch = createAppColumnHelper<OrganizationMember>();

export function MembersScreen({
  organizationId,
  canManage,
}: MembersScreenProps) {
  const {
    data: members,
    isPending,
    error,
  } = useOrganizationMembers(organizationId);

  const columns = [
    ch.accessor("email", { header: "Correo" }),
    ch.display({
      id: "rol",
      header: "Rol",
      cell: (cell) => (
        <Badge variant="secondary">
          {organizationRoleLabel(cell.row.original.role)}
        </Badge>
      ),
    }),
    ch.accessor("createdAt", {
      header: "Desde",
      cell: (cell) => formatDate(cell.getValue()),
    }),
    ...(canManage
      ? [
          ch.display({
            id: "acciones",
            header: "",
            meta: { align: "right" as const },
            cell: (cell: { row: { original: OrganizationMember } }) => (
              <MemberActions
                organizationId={organizationId}
                member={cell.row.original}
              />
            ),
          }),
        ]
      : []),
  ];

  return (
    <DataTable
      columns={ch.columns(columns)}
      page={toStaticPage(members)}
      isPending={isPending}
      error={error}
      state={STATIC_TABLE_STATE}
      onStateChange={() => {}}
      searchable={false}
      emptyState={{ title: "Sin miembros todavía." }}
      toolbarActions={
        canManage ? <AddMemberDialog organizationId={organizationId} /> : null
      }
      getRowId={(member) => member.platformUserId}
    />
  );
}

function MemberActions({
  organizationId,
  member,
}: {
  organizationId: string;
  member: OrganizationMember;
}) {
  const changeRole = useChangeOrganizationMemberRole(organizationId);
  const remove = useRemoveOrganizationMember(organizationId);
  const [confirmRemove, setConfirmRemove] = useState(false);

  const busy = changeRole.isPending || remove.isPending;

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger
          render={<Button variant="ghost" size="xs" aria-label="Acciones" />}
        >
          <MoreHorizontal />
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuGroup>
            <DropdownMenuLabel className="text-muted-foreground text-xs">
              Cambiar rol
            </DropdownMenuLabel>
            {ORGANIZATION_ROLE_OPTIONS.map((option) => (
              <DropdownMenuItem
                key={option.value}
                disabled={busy || option.value === member.role}
                onClick={() =>
                  changeRole.mutate({
                    userId: member.platformUserId,
                    role: option.value,
                  })
                }
              >
                {option.label}
              </DropdownMenuItem>
            ))}
          </DropdownMenuGroup>
          <DropdownMenuSeparator />
          <DropdownMenuItem
            disabled={busy}
            onClick={() => setConfirmRemove(true)}
          >
            Quitar de la organización
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <AlertDialog open={confirmRemove} onOpenChange={setConfirmRemove}>
        <AlertDialogContent size="sm">
          <AlertDialogHeader>
            <AlertDialogTitle>Quitar a {member.email}</AlertDialogTitle>
            <AlertDialogDescription>
              Pierde acceso a esta organización y a sus tenants. No se puede
              deshacer.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                setConfirmRemove(false);
                remove.mutate(member.platformUserId);
              }}
            >
              Quitar
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}

const schema = z.object({
  email: z
    .string()
    .min(1, "El correo es obligatorio.")
    .email("Correo inválido."),
  role: z.string().min(1, "El rol es obligatorio."),
});

type Values = z.infer<typeof schema>;

function AddMemberDialog({ organizationId }: { organizationId: string }) {
  const [open, setOpen] = useState(false);
  const add = useAddOrganizationMember(organizationId);

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { email: "", role: "member" },
  });
  const role = useController({ control: form.control, name: "role" });

  const submit = form.handleSubmit(async (values) => {
    try {
      await add.mutateAsync({ email: values.email.trim(), role: values.role });
      setOpen(false);
      form.reset();
    } catch (error) {
      if (!applyFieldErrors(error, form.setError)) {
        form.setError("email", { message: organizationErrorMessage(error) });
      }
    }
  });

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger render={<Button size="sm" className="h-8 gap-1.5" />}>
        <Plus /> Invitar miembro
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>Invitar miembro</DialogTitle>
          <DialogDescription>
            Tiene que ser ya un usuario de la plataforma — dado de alta con ese
            correo.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
          <Field>
            <FieldLabel htmlFor="member-email">Correo</FieldLabel>
            <Input id="member-email" type="email" {...form.register("email")} />
            <FieldError errors={[form.formState.errors.email]} />
          </Field>

          <Field>
            <FieldLabel htmlFor="member-role">Rol</FieldLabel>
            <Select
              items={selectItems(ORGANIZATION_ROLE_OPTIONS)}
              value={role.field.value}
              onValueChange={(next) => role.field.onChange(String(next))}
            >
              <SelectTrigger id="member-role" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {ORGANIZATION_ROLE_OPTIONS.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <FieldError errors={[form.formState.errors.role]} />
          </Field>

          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" />}>
              Cancelar
            </DialogClose>
            <Button type="submit" disabled={form.formState.isSubmitting}>
              Invitar
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
