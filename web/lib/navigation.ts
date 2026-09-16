import {
  Activity,
  Building2,
  LayoutDashboard,
  Settings2,
  SlidersHorizontal,
  Users,
  UsersRound,
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
   * Rango mínimo para verlo (`ROLE` en `features/auth/roles.ts`). **Esconder no es
   * proteger**: la API valida cada petición contra `platform_users`.
   */
  minRole: RoleLevel;
  /**
   * Techo opcional de rango. Sin él, cualquier rango `>= minRole` lo ve — lo normal
   * dentro de un mismo ámbito (un `admin_tenant` ve todo lo que ve un `emisor`). Pero
   * `admin_tenant` y `admin_sistema` no son el mismo ámbito con más o menos permiso:
   * uno administra *su* contribuyente, el otro administra la plataforma para *todos*
   * los contribuyentes. Un ítem que solo tiene sentido para "tu propio contribuyente"
   * (p. ej. `/configuracion`) pone `maxRole: ROLE.admin_tenant` para que no le
   * aparezca también a un Nemus Admin, que no tiene un contribuyente propio ahí.
   */
  maxRole?: RoleLevel;
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
        minRole: ROLE.consultor,
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
        minRole: ROLE.admin_tenant,
        maxRole: ROLE.admin_tenant,
        // Ejemplo de módulo previsto sin pantalla: se pinta inerte con la etiqueta
        // «pronto». Cámbialo a `true` cuando exista `app/(app)/usuarios/page.tsx`.
        ready: false,
      },
      {
        href: "/configuracion",
        label: "Configuración",
        description: "Los ajustes de tu facturación",
        icon: Settings2,
        minRole: ROLE.admin_tenant,
        maxRole: ROLE.admin_tenant,
        ready: true,
      },
    ],
  },
  {
    // Solo Nemus Admin: la config transversal de la plataforma para todos los
    // contribuyentes, no la de uno solo (esa es `/configuracion`, arriba).
    label: "Nemus Admin",
    items: [
      {
        href: "/nemus/operacion",
        label: "Operación",
        description:
          "Latido de los workers, los outbox y las secuencias, en vivo",
        icon: Activity,
        minRole: ROLE.admin_sistema,
        ready: true,
      },
      {
        href: "/nemus/tenants",
        label: "Contribuyentes",
        description:
          "Alta y gestión de contribuyentes: perfil, certificados, secuencias y API keys",
        icon: Building2,
        minRole: ROLE.admin_sistema,
        ready: true,
      },
      {
        href: "/nemus/usuarios",
        label: "Usuarios",
        description: "Quién entra a la plataforma y con qué rol",
        icon: UsersRound,
        minRole: ROLE.admin_sistema,
        ready: true,
      },
      {
        href: "/nemus/configuracion",
        label: "Configuración",
        description: "Los ajustes operativos de la plataforma",
        icon: SlidersHorizontal,
        minRole: ROLE.admin_sistema,
        ready: true,
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
