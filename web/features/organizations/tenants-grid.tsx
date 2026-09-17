"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { Building2, ChevronRight, Search } from "lucide-react";

import { EmptyState } from "@/components/shared/empty-state";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import type { TenantSummary } from "@/features/tenants/types";
import { selectItems } from "@/lib/select-items";

import { useOrganizationTenants } from "./use-organizations";

const STATUS_OPTIONS = [
  { value: "all", label: "Todos los estados" },
  { value: "Active", label: "Activo" },
  { value: "Suspended", label: "Suspendido" },
];

function statusLabel(status: string): string {
  return status === "Active" ? "Activo" : "Suspendido";
}

function matches(tenant: TenantSummary, query: string): boolean {
  const q = query.trim().toLowerCase();
  if (q === "") return true;

  return (
    tenant.legalName.toLowerCase().includes(q) ||
    tenant.rnc.toLowerCase().includes(q)
  );
}

/**
 * El grid de tenants de una organización — la vista principal de
 * `/org/[orgSlug]`. Solo Razón Social, RNC y estado por ahora: el ambiente,
 * el vencimiento del certificado y el último e-CF necesitarían enriquecer
 * `TenantSummaryDto` (o pegarle N+1 veces al backend, que además puede
 * fallar si sos miembro de la organización pero no de ese tenant puntual) —
 * queda para una siguiente pasada.
 */
export function TenantsGrid({ organizationId }: { organizationId: string }) {
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
            <Link
              key={tenant.id}
              href={`/tenant/${tenant.id}`}
              className="bg-card border-border/60 hover:bg-muted/50 flex items-center gap-3 rounded-2xl border p-4 shadow-xs transition-colors"
            >
              <div className="bg-muted text-muted-foreground flex size-9 shrink-0 items-center justify-center rounded-lg">
                <Building2 className="size-4" aria-hidden />
              </div>
              <div className="flex flex-1 flex-col gap-0.5">
                <span className="text-sm font-medium">{tenant.legalName}</span>
                <span className="text-muted-foreground font-mono text-xs">
                  {tenant.rnc}
                </span>
              </div>
              <Badge
                variant={tenant.status === "Active" ? "outline" : "secondary"}
              >
                {statusLabel(tenant.status)}
              </Badge>
              <ChevronRight
                className="text-muted-foreground size-4"
                aria-hidden
              />
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
