"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { Building2, ChevronRight, Search, X } from "lucide-react";

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
import { EmptyState } from "@/components/shared/empty-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import {
  ENVIRONMENT_COLOR,
  environmentLabel,
} from "@/features/tenants/options";
import { selectItems } from "@/lib/select-items";
import { cn } from "@/lib/utils";

import { useOrganizationTenants } from "./use-organizations";
import type { OrganizationTenantSummary } from "./types";

const STATUS_OPTIONS = [
  { value: "all", label: "Todos los estados" },
  { value: "Active", label: "Activo" },
  { value: "Suspended", label: "Suspendido" },
];

function statusLabel(status: string): string {
  return status === "Active" ? "Activo" : "Suspendido";
}

function matches(tenant: OrganizationTenantSummary, query: string): boolean {
  const q = query.trim().toLowerCase();
  if (q === "") return true;

  return (
    tenant.legalName.toLowerCase().includes(q) ||
    tenant.rnc.toLowerCase().includes(q)
  );
}

/**
 * El grid de tenants de una organización — la vista principal de
 * `/org/[orgSlug]`, reusado tal cual en la pestaña "Tenants" del detalle de
 * organización del operador (`/nemus/organizaciones/[id]`). A propósito
 * minimalista: solo Razón Social/RNC y los dos badges (estado, ambiente
 * DGII). Certificado y último e-CF quedan para el `/inicio` del tenant — son
 * datos operativos de ese contribuyente puntual, no algo que se compare de
 * un vistazo entre tenants de la organización.
 */
export function TenantsGrid({
  organizationId,
  linkTo = (tenantId) => `/tenant/${tenantId}`,
  onUnassign,
}: {
  organizationId: string;
  /**
   * A dónde enlaza cada tarjeta. Por defecto, el dashboard self-service del
   * tenant (`/tenant/[tenantId]`); el operador pasa `/nemus/tenants/[id]`,
   * que es donde vive su propia vista con pestañas de certificados/
   * secuencias/API keys.
   */
  linkTo?: (tenantId: string) => string;
  /**
   * Si viene, cada tarjeta gana un botón "Quitar" (con confirmación) — solo
   * el operador lo pasa. Self-service nunca puede desasociar un tenant de
   * su propia organización.
   */
  onUnassign?: (tenantId: string) => void;
}) {
  const { data, isPending, error } = useOrganizationTenants(organizationId);
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("all");

  const tenants = useMemo(() => {
    const items = data?.items ?? [];
    return items.filter(
      (tenant) =>
        matches(tenant, query) &&
        (status === "all" || tenant.status === status),
    );
  }, [data, query, status]);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
        <div className="relative flex-1">
          <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" />
          <Input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Buscar por razón social o RNC…"
            className="pl-9"
          />
        </div>

        <Select
          items={selectItems(STATUS_OPTIONS)}
          value={status}
          onValueChange={(next) => setStatus(String(next))}
        >
          <SelectTrigger className="w-full sm:w-48">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {STATUS_OPTIONS.map((option) => (
              <SelectItem key={option.value} value={option.value}>
                {option.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {isPending ? (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {Array.from({ length: 4 }, (_, i) => (
            <Skeleton key={i} className="h-20 rounded-2xl" />
          ))}
        </div>
      ) : error ? (
        <EmptyState
          title="No se pudieron cargar los tenants."
          description="Intentá de nuevo en un momento."
        />
      ) : tenants.length === 0 ? (
        <EmptyState
          icon={Building2}
          title={
            (data?.items.length ?? 0) === 0
              ? "Todavía no hay ningún tenant asociado a esta organización."
              : "Ningún tenant coincide con la búsqueda."
          }
        />
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {tenants.map((tenant) => (
            <div
              key={tenant.id}
              className="bg-card border-border/60 hover:bg-muted/50 relative flex items-center gap-3 rounded-2xl border p-4 shadow-xs transition-colors"
            >
              <Link
                href={linkTo(tenant.id)}
                className="absolute inset-0 rounded-2xl"
                aria-label={tenant.legalName}
              />

              <div className="bg-muted text-muted-foreground pointer-events-none flex size-9 shrink-0 items-center justify-center rounded-lg">
                <Building2 className="size-4" aria-hidden />
              </div>

              <div className="pointer-events-none flex flex-1 flex-col gap-0.5">
                <span className="text-sm font-medium">{tenant.legalName}</span>
                <span className="text-muted-foreground font-mono text-xs">
                  {tenant.rnc}
                </span>
              </div>

              <div className="pointer-events-none flex flex-col items-end gap-1">
                <Badge
                  variant={tenant.status === "Active" ? "outline" : "secondary"}
                >
                  {statusLabel(tenant.status)}
                </Badge>
                {tenant.defaultEnvironment && (
                  <Badge
                    variant="outline"
                    className={cn(
                      "font-normal",
                      ENVIRONMENT_COLOR[tenant.defaultEnvironment],
                    )}
                  >
                    {environmentLabel(tenant.defaultEnvironment)}
                  </Badge>
                )}
              </div>

              {onUnassign ? (
                <UnassignButton
                  tenant={tenant}
                  onConfirm={() => onUnassign(tenant.id)}
                />
              ) : (
                <ChevronRight
                  className="text-muted-foreground pointer-events-none size-4 shrink-0"
                  aria-hidden
                />
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function UnassignButton({
  tenant,
  onConfirm,
}: {
  tenant: OrganizationTenantSummary;
  onConfirm: () => void;
}) {
  const [confirm, setConfirm] = useState(false);

  return (
    <>
      <Button
        size="icon-sm"
        variant="ghost"
        className="relative z-10 shrink-0"
        aria-label={`Quitar ${tenant.legalName} de la organización`}
        onClick={(event) => {
          event.preventDefault();
          setConfirm(true);
        }}
      >
        <X className="text-muted-foreground size-4" aria-hidden />
      </Button>

      <AlertDialog open={confirm} onOpenChange={setConfirm}>
        <AlertDialogContent size="sm">
          <AlertDialogHeader>
            <AlertDialogTitle>Quitar de la organización</AlertDialogTitle>
            <AlertDialogDescription>
              «{tenant.legalName}» queda huérfano — sin organización dueña,
              hasta que se le asigne otra. No afecta su facturación ni sus
              datos.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                setConfirm(false);
                onConfirm();
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
