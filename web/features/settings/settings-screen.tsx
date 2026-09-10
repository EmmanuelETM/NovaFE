"use client";

import { ShieldAlert } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { ApiError } from "@/lib/api/problem";

import { groupSettings } from "./setting-groups";
import { SettingRow } from "./setting-row";
import { usePlatformSettings } from "./use-platform-settings";

export function SettingsScreen() {
  const { data, isPending, error } = usePlatformSettings();

  if (isPending) return <LoadingState />;

  if (error) {
    if (error instanceof ApiError && error.isAccessDenied) {
      return (
        <Alert>
          <ShieldAlert />
          <AlertTitle>Necesitas rol de operador</AlertTitle>
          <AlertDescription>
            Esta pantalla es para operadores del SaaS. Tu cuenta no tiene ese
            acceso.
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
        No hay ajustes declarados todavía.
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
          {Array.from({ length: 3 }).map((_, index) => (
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
