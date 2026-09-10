"use client";

import { useState } from "react";
import { MoreHorizontal } from "lucide-react";

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
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Field, FieldLabel } from "@/components/ui/field";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { selectItems } from "@/lib/select-items";

import { TENANT_ROLE_OPTIONS } from "./role-options";
import { isActive, type PlatformUser } from "./types";
import {
  useChangeUserRole,
  useReinstateUser,
  useRevokeUser,
  type UserScope,
} from "./use-users";

export function UserActions({
  user,
  tenantId,
}: {
  user: PlatformUser;
  tenantId: string | null;
}) {
  const scope: UserScope = { userId: user.id, tenantId };
  const active = isActive(user);

  const revoke = useRevokeUser();
  const reinstate = useReinstateUser();
  const changeRole = useChangeUserRole();

  const [confirm, setConfirm] = useState(false);
  const [roleOpen, setRoleOpen] = useState(false);
  const [role, setRole] = useState(user.role);

  const busy = revoke.isPending || reinstate.isPending || changeRole.isPending;

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger
          render={<Button variant="ghost" size="xs" aria-label="Acciones" />}
        >
          <MoreHorizontal />
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          {tenantId !== null && (
            <DropdownMenuItem
              onClick={() => {
                setRole(user.role);
                setRoleOpen(true);
              }}
              disabled={busy}
            >
              Cambiar rol
            </DropdownMenuItem>
          )}
          <DropdownMenuItem onClick={() => setConfirm(true)} disabled={busy}>
            {active ? "Revocar acceso" : "Reactivar acceso"}
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <AlertDialog open={confirm} onOpenChange={setConfirm}>
        <AlertDialogContent size="sm">
          <AlertDialogHeader>
            <AlertDialogTitle>
              {active ? "Revocar el acceso" : "Reactivar el acceso"}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {active
                ? `«${user.email}» dejará de poder entrar al dashboard.`
                : `«${user.email}» volverá a poder entrar al dashboard.`}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                setConfirm(false);
                if (active) revoke.mutate(scope);
                else reinstate.mutate(scope);
              }}
            >
              {active ? "Revocar" : "Reactivar"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {tenantId !== null && (
        <Dialog open={roleOpen} onOpenChange={setRoleOpen}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Cambiar el rol de «{user.email}»</DialogTitle>
            </DialogHeader>
            <Field>
              <FieldLabel htmlFor="change-role">Rol</FieldLabel>
              <Select
                items={selectItems(TENANT_ROLE_OPTIONS)}
                value={role}
                onValueChange={(next) => setRole(String(next))}
              >
                <SelectTrigger id="change-role" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {TENANT_ROLE_OPTIONS.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            <DialogFooter>
              <DialogClose render={<Button variant="outline" />}>
                Cancelar
              </DialogClose>
              <Button
                disabled={busy || role === user.role}
                onClick={() => {
                  setRoleOpen(false);
                  changeRole.mutate({ tenantId, userId: user.id, role });
                }}
              >
                Guardar
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}
    </>
  );
}
