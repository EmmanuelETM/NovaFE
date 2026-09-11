"use client";

import { useState } from "react";
import { Lock, TriangleAlert } from "lucide-react";

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
import { Label } from "@/components/ui/label";

import { formatDate } from "@/lib/format";

import { SettingControl } from "./setting-control";
import { SettingHistoryDialog } from "./setting-history-dialog";
import type { PlatformSetting } from "./types";
import {
  useResetPlatformSetting,
  useUpdatePlatformSetting,
} from "./use-platform-settings";

/**
 * Una fila de la pantalla: la metadata del setting + su control + los botones.
 *
 * El valor en edición vive en estado local (`draft`); se sincroniza con el del
 * servidor cuando ese cambia. «Guardar» aparece solo cuando difiere. Un
 * kill-switch pide confirmación antes de guardar; volver al default también.
 */
export function SettingRow({ setting }: { setting: PlatformSetting }) {
  const update = useUpdatePlatformSetting();
  const reset = useResetPlatformSetting();

  // El valor en edición. Se resincroniza con el del servidor cuando ese cambia
  // (tras guardar, o si otro operador lo movió) — patrón «ajustar estado al
  // cambiar una prop» de React, sin efecto.
  const [draft, setDraft] = useState(setting.effectiveValue);
  const [serverValue, setServerValue] = useState(setting.effectiveValue);
  if (serverValue !== setting.effectiveValue) {
    setServerValue(setting.effectiveValue);
    setDraft(setting.effectiveValue);
  }

  const [confirmSave, setConfirmSave] = useState(false);
  const [confirmReset, setConfirmReset] = useState(false);

  const dirty = draft !== setting.effectiveValue;
  const busy = update.isPending || reset.isPending;
  const controlId = `setting-${setting.key}`;

  const needsConfirm = setting.killSwitch || setting.sensitive;

  const save = () => {
    setConfirmSave(false);
    update.mutate({ key: setting.key, value: draft, confirm: true });
  };

  const onSaveClick = () => {
    if (needsConfirm) setConfirmSave(true);
    else save();
  };

  return (
    <div className="flex flex-col gap-3 py-4 sm:flex-row sm:items-start sm:justify-between sm:gap-6">
      <div className="flex flex-col gap-1">
        <div className="flex flex-wrap items-center gap-2">
          <Label htmlFor={controlId}>{setting.label}</Label>
          {setting.killSwitch && (
            <Badge
              variant="outline"
              className="border-amber-500 text-amber-600 dark:text-amber-500"
            >
              <TriangleAlert /> interruptor
            </Badge>
          )}
          {setting.sensitive && (
            <Lock
              className="text-muted-foreground size-3.5"
              aria-label="Setting sensible"
            />
          )}
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
              <Button size="xs" onClick={onSaveClick} disabled={busy}>
                Guardar
              </Button>
            </>
          ) : (
            setting.isOverridden && (
              <Button
                size="xs"
                variant="ghost"
                onClick={() => setConfirmReset(true)}
                disabled={busy}
              >
                Restablecer
              </Button>
            )
          )}
        </div>
      </div>

      <AlertDialog open={confirmSave} onOpenChange={setConfirmSave}>
        <AlertDialogContent size="sm">
          <AlertDialogHeader>
            <AlertDialogTitle>Cambiar un ajuste crítico</AlertDialogTitle>
            <AlertDialogDescription>
              «{setting.label}» afecta el comportamiento de toda la plataforma.
              El cambio surte efecto en segundos.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction onClick={save}>Cambiar</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={confirmReset} onOpenChange={setConfirmReset}>
        <AlertDialogContent size="sm">
          <AlertDialogHeader>
            <AlertDialogTitle>Volver al valor por defecto</AlertDialogTitle>
            <AlertDialogDescription>
              «{setting.label}» pasará a «{setting.defaultValue}».
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                setConfirmReset(false);
                reset.mutate({ key: setting.key, confirm: true });
              }}
            >
              Restablecer
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
