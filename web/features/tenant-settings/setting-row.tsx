"use client";

import { useState } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";

import { formatDate } from "@/lib/format";

import { SettingControl } from "./setting-control";
import { SettingHistoryDialog } from "./setting-history-dialog";
import type { TenantSetting } from "./types";
import {
  useResetTenantSetting,
  useUpdateTenantSetting,
} from "./use-tenant-settings";

/**
 * Una fila de la pantalla: la metadata del setting + su control + los botones.
 *
 * El valor en edición vive en estado local (`draft`); se resincroniza con el del
 * servidor cuando ese cambia (patrón «ajustar estado al cambiar una prop», sin
 * efecto). «Guardar» aparece solo cuando difiere.
 */
export function SettingRow({ setting }: { setting: TenantSetting }) {
  const update = useUpdateTenantSetting();
  const reset = useResetTenantSetting();

  const [draft, setDraft] = useState(setting.effectiveValue);
  const [serverValue, setServerValue] = useState(setting.effectiveValue);
  if (serverValue !== setting.effectiveValue) {
    setServerValue(setting.effectiveValue);
    setDraft(setting.effectiveValue);
  }

  const dirty = draft !== setting.effectiveValue;
  const busy = update.isPending || reset.isPending;
  const controlId = `setting-${setting.key}`;

  return (
    <div className="flex flex-col gap-3 py-4 sm:flex-row sm:items-start sm:justify-between sm:gap-6">
      <div className="flex flex-col gap-1">
        <div className="flex flex-wrap items-center gap-2">
          <Label htmlFor={controlId}>{setting.label}</Label>
          {setting.isOverridden && (
            <Badge variant="secondary">modificado</Badge>
          )}
        </div>

        {setting.description && (
          <p className="text-muted-foreground text-sm">{setting.description}</p>
        )}

        <p className="text-muted-foreground text-xs">
          Por defecto: <code className="font-mono">{setting.defaultValue}</code>
          {setting.constraints && <> · {setting.constraints}</>}
          {setting.unit && <> · {setting.unit}</>}
        </p>

        {setting.isOverridden && (
          <div className="text-muted-foreground flex flex-wrap items-center gap-x-2 text-xs">
            {setting.updatedAt && (
              <span>
                Modificado
                {setting.updatedBy && <> por {setting.updatedBy}</>} ·{" "}
                {formatDate(setting.updatedAt)}
              </span>
            )}
            <SettingHistoryDialog setting={setting} />
          </div>
        )}
      </div>

      <div className="flex shrink-0 flex-col items-start gap-2 sm:items-end">
        <SettingControl
          setting={setting}
          value={draft}
          onChange={setDraft}
          disabled={busy}
          id={controlId}
        />

        <div className="flex items-center gap-2">
          {dirty ? (
            <>
              <Button
                size="xs"
                variant="ghost"
                onClick={() => setDraft(setting.effectiveValue)}
                disabled={busy}
              >
                Cancelar
              </Button>
              <Button
                size="xs"
                onClick={() =>
                  update.mutate({ key: setting.key, value: draft })
                }
                disabled={busy}
              >
                Guardar
              </Button>
            </>
          ) : (
            setting.isOverridden && (
              <Button
                size="xs"
                variant="ghost"
                onClick={() => reset.mutate({ key: setting.key })}
                disabled={busy}
              >
                Restablecer
              </Button>
            )
          )}
        </div>
      </div>
    </div>
  );
}
