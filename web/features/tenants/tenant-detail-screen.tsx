"use client";

import Link from "next/link";
import { ExternalLink } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";

import { ApiKeysTab } from "./api-keys-tab";
import { CertificatesTab } from "./certificates-tab";
import { EmitterProfileTab } from "./emitter-profile-tab";
import { SequencesTab } from "./sequences-tab";
import { useTenant } from "./use-tenants";

export function TenantDetailScreen({ tenantId }: { tenantId: string }) {
  const { data: tenant, isPending, error } = useTenant(tenantId);

  if (isPending) {
    return <p className="text-muted-foreground text-sm">Cargando…</p>;
  }

  if (error || !tenant) {
    return (
      <p className="text-destructive text-sm">
        No se pudo cargar este contribuyente.
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-xl font-semibold">{tenant.legalName}</h2>
        <Badge variant="secondary">{tenant.rnc}</Badge>
        <Badge variant="outline">{tenant.plan}</Badge>
        <Badge variant={tenant.status === "Active" ? "outline" : "secondary"}>
          {tenant.status}
        </Badge>
      </div>

      <Tabs defaultValue="perfil">
        <TabsList>
          <TabsTrigger value="perfil">Perfil</TabsTrigger>
          <TabsTrigger value="certificados">Certificados</TabsTrigger>
          <TabsTrigger value="secuencias">Secuencias</TabsTrigger>
          <TabsTrigger value="api-keys">API Keys</TabsTrigger>
          <TabsTrigger value="usuarios">Usuarios</TabsTrigger>
        </TabsList>

        <TabsContent value="perfil">
          <EmitterProfileTab tenantId={tenantId} />
        </TabsContent>

        <TabsContent value="certificados">
          <CertificatesTab tenantId={tenantId} />
        </TabsContent>

        <TabsContent value="secuencias">
          <SequencesTab tenantId={tenantId} />
        </TabsContent>

        <TabsContent value="api-keys">
          <ApiKeysTab tenantId={tenantId} />
        </TabsContent>

        <TabsContent value="usuarios">
          <Button
            variant="outline"
            render={
              <Link
                href={`/plataforma/usuarios?vista=contribuyente&tenant=${tenantId}`}
              />
            }
          >
            <ExternalLink /> Ver usuarios de este contribuyente
          </Button>
        </TabsContent>
      </Tabs>
    </div>
  );
}
