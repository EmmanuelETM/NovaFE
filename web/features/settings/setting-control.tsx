"use client";

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Input } from "@/components/ui/input";
import { selectItems } from "@/lib/select-items";

import type { PlatformSetting } from "./types";

interface SettingControlProps {
  setting: PlatformSetting;
  /** El valor en edición (siempre texto, como lo espera la API). */
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  /** Para asociar la etiqueta de la fila. */
  id?: string;
}

/**
 * El control adecuado para el tipo del setting. Trabaja siempre con texto: la
 * API valida y canoniza el valor, la pantalla no calcula nada.
 */
export function SettingControl({
  setting,
  value,
  onChange,
  disabled,
  id,
}: SettingControlProps) {
  switch (setting.valueType) {
    case "boolean":
      return (
        <Switch
          id={id}
          checked={value === "true"}
          onCheckedChange={(checked) => onChange(checked ? "true" : "false")}
          disabled={disabled}
        />
      );

    case "option": {
      const options = setting.options ?? [];
      return (
        <Select
          items={selectItems(options.map((o) => ({ value: o, label: o })))}
          value={value}
          onValueChange={(next) => onChange(String(next))}
          disabled={disabled}
        >
          <SelectTrigger id={id} size="sm" className="w-44">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {options.map((option) => (
              <SelectItem key={option} value={option}>
                {option}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      );
    }

    case "integer":
    case "decimal":
      return (
        <Input
          id={id}
          type="number"
          className="w-44"
          value={value}
          min={setting.min ?? undefined}
          max={setting.max ?? undefined}
          step={setting.valueType === "integer" ? 1 : "any"}
          onChange={(event) => onChange(event.target.value)}
          disabled={disabled}
        />
      );

    default:
      // `duration`, `string`, y cualquier tipo nuevo que aún no tenga control propio.
      return (
        <Input
          id={id}
          type="text"
          className="w-44"
          value={value}
          onChange={(event) => onChange(event.target.value)}
          disabled={disabled}
        />
      );
  }
}
