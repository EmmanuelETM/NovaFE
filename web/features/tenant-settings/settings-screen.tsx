"use client";

import { ShieldAlert } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { ApiError } from "@/lib/api/problem";

import { SettingRow } from "./setting-row";
import type { TenantSetting } from "./types";
import { useTenantSettings } from "./use-tenant-settings";

interface SettingGroup {
  label: string;
  settings: TenantSetting[];
}

/** Agrupa por `group`, conservando el orden en que llegan de la API. */
function groupSettings(settings: TenantSetting[]): SettingGroup[] {
  const groups: SettingGroup[] = [];

  for (const setting of settings) {
    if (setting.deprecated) continue;

    const existing = groups.find((group) => group.label === setting.group);
    if (existing) existing.settings.push(setting);
    else groups.push({ label: setting.group, settings: [setting] });
  }

  return groups;
}

export function SettingsScreen() {
  const { data, isPending, error } = useTenantSettings();

  if (isPending) return <LoadingState />;

  if (error) {
    if (error instanceof ApiError && error.isAccessDenied) {
      return (
        <Alert>
          <ShieldAlert />
          <AlertTitle>Necesitas rol de administrador</AlertTitle>
          <AlertDescription>
            Solo el administrador del contribuyente puede ver y cambiar esta
            configuración.
          </AlertDescription>
        </Alert>
      );
    }

    return (
      <Alert variant="destructive">
        <ShieldAlert />
        <AlertTitle>No se pudieron cargar los ajustes</AlertTitle>
        <AlertDescription>
          {error instanceof Error ? error.message : "Error desconocido."}
        </AlertDescription>
      </Alert>
    );
  }

  const groups = groupSettings(data);

  if (groups.length === 0) {
    return (
      <p className="text-muted-foreground text-sm">
        No hay ajustes disponibles todavía.
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      {groups.map((group) => (
        <Card key={group.label}>
          <CardHeader>
            <CardTitle>{group.label}</CardTitle>
          </CardHeader>
          <CardContent className="divide-border divide-y py-0">
            {group.settings.map((setting) => (
              <SettingRow key={setting.key} setting={setting} />
            ))}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

function LoadingState() {
  return (
    <div className="flex flex-col gap-6">
      <Card>
        <CardHeader>
          <Skeleton className="h-5 w-32" />
        </CardHeader>
        <CardContent className="flex flex-col gap-4 py-0 pb-5">
          {Array.from({ length: 2 }).map((_, index) => (
            <div key={index} className="flex justify-between gap-6 py-2">
              <div className="flex flex-col gap-2">
                <Skeleton className="h-4 w-40" />
                <Skeleton className="h-3 w-64" />
              </div>
              <Skeleton className="h-8 w-44" />
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
