import { create } from "zustand";

/**
 * Si la paleta de comandos (`⌘K`/`Ctrl+K`) está abierta.
 *
 * Único uso de Zustand en el proyecto: no hay URL ni caché de servidor que
 * sea dueña natural de "está abierta la paleta ahora" — es puramente del
 * cliente y global a la ventana, así que amerita la store (ver la regla en
 * `web/CLAUDE.md`: "si dudas entre Zustand y la URL, es la URL" — el tenant
 * y la organización activos siguen saliendo de la URL, nunca de acá).
 */
interface CommandPaletteState {
  open: boolean;
  setOpen: (open: boolean) => void;
  toggle: () => void;
}

export const useCommandPaletteStore = create<CommandPaletteState>((set) => ({
  open: false,
  setOpen: (open) => set({ open }),
  toggle: () => set((state) => ({ open: !state.open })),
}));
