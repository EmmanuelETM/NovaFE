"use client";

import { useState } from "react";
import { History } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { formatDateTime } from "@/lib/format";

import type { PlatformSetting } from "./types";
import { usePlatformSettingHistory } from "./use-platform-setting-history";

/** El default se muestra como "por defecto"; `null` = vuelta al default. */
function display(value: string | null, fallback: string): string {
  return value ?? fallback;
}

export function SettingHistoryDialog({
  setting,
}: {
  setting: PlatformSetting;
}) {
  const [open, setOpen] = useState(false);
  const { data, isPending, error } = usePlatformSettingHistory(
    setting.key,
    open,
  );

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger render={<Button size="xs" variant="ghost" />}>
        <History /> Historial
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>Historial de «{setting.label}»</DialogTitle>
          <DialogDescription>
            Cada cambio del override, el más reciente primero.
          </DialogDescription>
        </DialogHeader>

        {isPending ? (
          <div className="flex flex-col gap-3">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
          </div>
        ) : error ? (
          <p className="text-destructive text-sm">
            {error instanceof Error ? error.message : "No se pudo cargar."}
          </p>
        ) : data && data.length > 0 ? (
          <ol className="flex flex-col gap-3">
            {data.map((change, index) => (
              <li
                key={`${change.changedAt}-${index}`}
                className="border-border flex flex-col gap-0.5 border-l-2 pl-3 text-sm"
              >
                <span className="font-mono">
                  {display(
                    change.previousValue,
                    `${setting.defaultValue} (default)`,
                  )}{" "}
                  →{" "}
                  {display(
                    change.newValue,
                    `${setting.defaultValue} (default)`,
                  )}
                </span>
                <span className="text-muted-foreground text-xs">
                  {formatDateTime(change.changedAt)}
                  {change.changedBy && <> · {change.changedBy}</>}
                </span>
              </li>
            ))}
          </ol>
        ) : (
          <p className="text-muted-foreground text-sm">
            Sin cambios registrados.
          </p>
        )}
      </DialogContent>
    </Dialog>
  );
}
