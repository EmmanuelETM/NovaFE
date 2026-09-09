import {
  LayoutDashboard,
  Settings2,
  Users,
  type LucideIcon,
} from "lucide-react";

import { ROLE, type RoleLevel } from "@/features/auth/roles";

/**
 * La navegación de la aplicación, en un solo lugar.
 *
 * De aquí sale el sidebar **y** el título de la barra superior y el de cada pantalla
 * (`PageHeader`). Que sean la misma definición no es ahorro de código: si el enlace dice
 * una cosa y la cabecera dice otra, el usuario no sabe si llegó a donde quería.
 *
 * Agregar una pantalla son dos pasos: una entrada aquí y un `page.tsx` bajo `app/(app)/`
 * con la misma ruta.
 */
export interface NavItem {
  /** Ruta. Como es visible, se escribe en el idioma de la interfaz. */
  href: string;
  label: string;
  /** Frase corta para la cabecera. Contesta «¿qué estoy viendo?». */
  description: string;
  icon: LucideIcon;
  /**
   * Nivel mínimo para verlo. Los roles son jerárquicos, así que pedir supervisor incluye
   * al administrador. **Esconder no es proteger**: la API valida cada petición.
   */
  minRole: RoleLevel;
  /**
   * Si la pantalla ya existe. Lo que está en `false` se pinta deshabilitado y no enlaza:
   * el módulo está previsto pero su interfaz no se ha construido, y mostrarlo comunica la
   * forma de la aplicación sin ofrecer un camino a ningún lado.
   */
  ready: boolean;
}

export interface NavSection {
  label: string;
  items: readonly NavItem[];
}

export const NAVIGATION: readonly NavSection[] = [
  {
    label: "General",
    items: [
      {
        href: "/",
        label: "Inicio",
        description: "El resumen de la aplicación",
        icon: LayoutDashboard,
        minRole: ROLE.operator,
        ready: true,
      },
    ],
  },
  {
    label: "Administración",
    items: [
      {
        href: "/usuarios",
        label: "Usuarios",
        description: "Quién entra al sistema y con qué rol",
        icon: Users,
        minRole: ROLE.administrator,
        // Ejemplo de módulo previsto sin pantalla: se pinta inerte con la etiqueta
        // «pronto». Cámbialo a `true` cuando exista `app/(app)/usuarios/page.tsx`.
        ready: false,
      },
      {
        href: "/configuracion",
        label: "Configuración",
        description: "Los valores que ajustan el comportamiento",
        icon: Settings2,
        minRole: ROLE.administrator,
        ready: false,
      },
    ],
  },
];

/** Todos los destinos, sin los grupos. */
export const NAV_ITEMS: readonly NavItem[] = NAVIGATION.flatMap(
  (section) => section.items,
);

/** La primera pantalla construida, que es a donde va la raíz. */
export const HOME_HREF: string =
  NAV_ITEMS.find((item) => item.ready)?.href ?? "/";

/**
 * El destino al que corresponde una ruta.
 *
 * Coincide por prefijo y se queda con **el más largo**, para que el detalle de algo
 * —`/usuarios/42`— siga resolviendo a su sección en vez de a la raíz.
 */
export function findNavItem(pathname: string): NavItem | undefined {
  let mejor: NavItem | undefined;

  for (const item of NAV_ITEMS) {
    const coincide =
      pathname === item.href || pathname.startsWith(`${item.href}/`);

    if (
      coincide &&
      (mejor === undefined || item.href.length > mejor.href.length)
    )
      mejor = item;
  }

  return mejor;
}
