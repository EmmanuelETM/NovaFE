"use client";

import { useState } from "react";

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
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { PLAN_OPTIONS } from "@/features/tenants/options";
import { selectItems } from "@/lib/select-items";

import { MembersScreen } from "./members-screen";
import { TenantsGrid } from "./tenants-grid";
import {
  useActivateOrganization,
  useOrganization,
  useSuspendOrganization,
  useUpdateOrganizationPlan,
} from "./use-organizations";

function statusLabel(status: string): string {
  return status === "Active" ? "Activa" : "Suspendida";
}

export function OrganizationDetailScreen({
  organizationId,
}: {
  organizationId: string;
}) {
  const {
    data: organization,
    isPending,
    error,
  } = useOrganization(organizationId);

  if (isPending) {
    return (
      <div className="flex flex-col gap-6">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-64 w-full rounded-2xl" />
      </div>
    );
  }

  if (error || !organization) {
    return (
      <p className="text-destructive text-sm">
        No se pudo cargar esta organización.
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-xl font-semibold">{organization.name}</h2>
        <Badge variant="secondary" className="font-mono">
          {organization.slug}
        </Badge>
        <Badge
          variant={organization.status === "Active" ? "outline" : "secondary"}
        >
          {statusLabel(organization.status)}
        </Badge>

        <div className="ml-auto flex items-center gap-2">
          <PlanSelect
            organizationId={organizationId}
            plan={organization.plan}
          />
          <SuspendActivateButton
            organizationId={organizationId}
            status={organization.status}
          />
        </div>
      </div>

      <Tabs defaultValue="miembros">
        <TabsList>
          <TabsTrigger value="miembros">Miembros</TabsTrigger>
          <TabsTrigger value="tenants">Tenants</TabsTrigger>
        </TabsList>

        <TabsContent value="miembros">
          <MembersScreen organizationId={organizationId} canManage />
        </TabsContent>

        <TabsContent value="tenants">
          <TenantsGrid
            organizationId={organizationId}
            linkTo={(tenantId) => `/nemus/tenants/${tenantId}`}
          />
        </TabsContent>
      </Tabs>
    </div>
  );
}

function PlanSelect({
  organizationId,
  plan,
}: {
  organizationId: string;
  plan: string;
}) {
  const updatePlan = useUpdateOrganizationPlan(organizationId);

  return (
    <Select
      items={selectItems(PLAN_OPTIONS)}
      value={plan}
      onValueChange={(next) => {
        if (next !== plan) updatePlan.mutate(String(next));
      }}
    >
      <SelectTrigger className="h-8 w-40" disabled={updatePlan.isPending}>
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {PLAN_OPTIONS.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

function SuspendActivateButton({
  organizationId,
  status,
}: {
  organizationId: string;
  status: string;
}) {
  const suspend = useSuspendOrganization(organizationId);
  const activate = useActivateOrganization(organizationId);
  const [confirmSuspend, setConfirmSuspend] = useState(false);

  if (status !== "Active") {
    return (
      <Button
        size="sm"
        variant="outline"
        className="h-8"
        disabled={activate.isPending}
        onClick={() => activate.mutate()}
      >
        Reactivar
      </Button>
    );
  }

  return (
    <>
      <Button
        size="sm"
        variant="outline"
        className="h-8"
        onClick={() => setConfirmSuspend(true)}
      >
        Suspender
      </Button>

      <AlertDialog open={confirmSuspend} onOpenChange={setConfirmSuspend}>
        <AlertDialogContent size="sm">
          <AlertDialogHeader>
            <AlertDialogTitle>Suspender organización</AlertDialogTitle>
            <AlertDialogDescription>
              Bloquea en cascada a todos los tenants de esta organización —
              dejan de poder emitir e-CF hasta que se reactive.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                setConfirmSuspend(false);
                suspend.mutate();
              }}
            >
              Suspender
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
