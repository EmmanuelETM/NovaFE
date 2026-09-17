import {
  Activity,
  Building,
  Building2,
  CreditCard,
  LayoutDashboard,
  ListOrdered,
  Receipt,
  Settings2,
  ShieldCheck,
  SlidersHorizontal,
  Users,
  UsersRound,
  Webhook,
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
  /**
   * No se pinta como grupo del sidebar principal — su contenido es del
   * contribuyente (empresa, certificados, plan…), no del día a día, así que
   * vive en el menú desplegable de `NavTenant`, al pie del sidebar, en vez
   * de competir por espacio con Inicio/Comprobantes. Sigue formando parte
   * de `NAVIGATION`/`NAV_ITEMS`: `PageHeader` y la miga de pan siguen
   * resolviendo estas rutas igual.
   */
  hideFromSidebar?: boolean;
  /**
   * A qué ámbito pertenece (Fase 3): `"tenant"` (default) vive bajo
   * `/tenant/[tenantId]/...`; `"operator"` es `/nemus/...`, sin tenant.
   * `NavMain` pinta uno u otro según si hay un tenant activo — sin esto, un
   * operador (que también satisface `minRole: consultor` de "General",
   * sin techo) vería "Inicio"/"Comprobantes" aunque no tenga tenant.
   */
  scope?: "tenant" | "operator";
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
      {
        href: "/comprobantes",
        label: "Comprobantes",
        description:
          "Los e-CF emitidos, su estado y su intercambio con la DGII",
        icon: Receipt,
        minRole: ROLE.consultor,
        ready: true,
      },
    ],
  },
  {
    label: "Administración",
    hideFromSidebar: true,
    items: [
      {
        href: "/empresa",
        label: "Empresa",
        description: "Los datos fiscales de tu contribuyente",
        icon: Building,
        minRole: ROLE.admin_tenant,
        maxRole: ROLE.admin_tenant,
        ready: true,
      },
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
      {
        href: "/certificados",
        label: "Certificados",
        description: "Los certificados digitales de tu contribuyente",
        icon: ShieldCheck,
        minRole: ROLE.admin_tenant,
        maxRole: ROLE.admin_tenant,
        ready: true,
      },
      {
        href: "/secuencias",
        label: "Secuencias",
        description: "Los rangos de e-NCF autorizados por la DGII",
        icon: ListOrdered,
        minRole: ROLE.admin_tenant,
        maxRole: ROLE.admin_tenant,
        ready: true,
      },
      {
        href: "/webhooks",
        label: "Webhooks",
        description: "A dónde y de qué te avisamos en tiempo real",
        icon: Webhook,
        minRole: ROLE.admin_tenant,
        maxRole: ROLE.admin_tenant,
        ready: true,
      },
      {
        href: "/plan",
        label: "Plan",
        // A propósito no dice "facturación": en un sistema de facturación
        // electrónica esa palabra se lee como "mis e-CF", no como "lo que le
        // pago a Nemus". "Plan" es corto y no compite con eso.
        description: "Tu plan de NovaFE, uso y método de pago",
        icon: CreditCard,
        minRole: ROLE.admin_tenant,
        maxRole: ROLE.admin_tenant,
        ready: false,
      },
    ],
  },
  {
    // Solo Nemus Admin: la config transversal de la plataforma para todos los
    // contribuyentes, no la de uno solo (esa es `/configuracion`, arriba).
    label: "Nemus Admin",
    scope: "operator",
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

/**
 * La ruta real de un `NavItem` (Fase 3).
 *
 * `href` en `NAVIGATION` es relativo al tenant activo (`/`, `/comprobantes`,
 * `/empresa`…) — salvo `/nemus/...`, que es de operador y no lleva tenant.
 * Esta función es el único lugar que sabe anteponer `/tenant/[tenantId]`.
 */
export function tenantHref(tenantId: string, href: string): string {
  if (href.startsWith("/nemus")) return href;

  return href === "/" ? `/tenant/${tenantId}` : `/tenant/${tenantId}${href}`;
}

/** Quita el prefijo `/tenant/[tenantId]` de un pathname, si lo tiene. */
function stripTenantPrefix(pathname: string): string {
  const withoutTenant = pathname.replace(/^\/tenant\/[^/]+/, "");
  return withoutTenant === "" ? "/" : withoutTenant;
}

/** Los items de una sección que un rango de rol puede ver. Mismo filtro que usan `NavMain` y `NavTenant`. */
export function visibleNavItems(
  items: readonly NavItem[],
  role: number,
): NavItem[] {
  return items.filter(
    (item) =>
      role >= item.minRole &&
      (item.maxRole === undefined || role <= item.maxRole),
  );
}

/**
 * El destino al que corresponde una ruta.
 *
 * Coincide por prefijo y se queda con **el más largo**, para que el detalle de algo
 * —`/usuarios/42`— siga resolviendo a su sección en vez de a la raíz.
 */
export function findNavItem(pathname: string): NavItem | undefined {
  const normalizado = stripTenantPrefix(pathname);
  let mejor: NavItem | undefined;

  for (const item of NAV_ITEMS) {
    const coincide =
      normalizado === item.href || normalizado.startsWith(`${item.href}/`);

    if (
      coincide &&
      (mejor === undefined || item.href.length > mejor.href.length)
    )
      mejor = item;
  }

  return mejor;
}
