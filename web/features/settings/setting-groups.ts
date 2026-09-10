import type { PlatformSetting } from "./types";

export interface SettingGroup {
  label: string;
  settings: PlatformSetting[];
}

/**
 * Agrupa los settings por su `group`, conservando el orden en que llegan de la
 * API (ya viene ordenado por grupo y etiqueta). Los settings `deprecated` se
 * descartan: siguen resolviendo del lado del servidor, pero el operador no
 * necesita verlos.
 */
export function groupSettings(settings: PlatformSetting[]): SettingGroup[] {
  const groups: SettingGroup[] = [];

  for (const setting of settings) {
    if (setting.deprecated) continue;

    const existing = groups.find((group) => group.label === setting.group);

    if (existing) existing.settings.push(setting);
    else groups.push({ label: setting.group, settings: [setting] });
  }

  return groups;
}
