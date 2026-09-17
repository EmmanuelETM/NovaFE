import {
  Activity,
  Boxes,
  Building,
  Building2,
  CreditCard,
  Key,
  LayoutDashboard,
  ListOrdered,
  Receipt,
  ScrollText,
  Settings2,
  ShieldCheck,
  SlidersHorizontal,
  UserRound,
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
 * Agregar una pantalla son dos pasos: una entrada aquí y un `page.tsx` bajo `app/tenant/[tenantId]/`
 * (o `app/org/[orgSlug]/`, o `app/(app)/nemus/`) con la misma ruta.
 */
export interface NavItem {
  /** Ruta relativa al scope (tenant/organización) u operador. Como es visible, en español. */
  href: string;
  label: string;
  /** Frase corta para la cabecera. Contesta «¿qué estoy viendo?». */
  description: string;
  icon: LucideIcon;
  /**
   * Rango mínimo de rol de **plataforma** para verlo (`ROLE` en
   * `features/auth/roles.ts`). Las secciones `scope: "organization"` no lo
   * usan de verdad (todas quedan en `ROLE.consultor`): el rol que manda ahí
   * es el de la organización (`owner`/`admin`/`member`, en
   * `UserOrganizationDto.role`), que cada pantalla resuelve por su cuenta —
   * no hay dos jerarquías de rango comparables en una sola prop.
   * **Esconder no es proteger**: la API valida cada petición.
   */
  minRole: RoleLevel;
  /**
   * Techo opcional de rango de plataforma. Sin él, cualquier rango `>= minRole` lo ve.
   * Un ítem que solo tiene sentido para "tu propio contribuyente" (p. ej. `/empresa`)
   * pone `maxRole: ROLE.admin_tenant` para que no le aparezca también a un Nemus Admin,
   * que no tiene un contribuyente propio ahí.
   */
  maxRole?: RoleLevel;
  /**
   * Si la pantalla ya existe. Lo que está en `false` se pinta deshabilitado y no enlaza:
   * el módulo está previsto pero su interfaz —o su autorización self-service— no se ha
   * construido, y mostrarlo comunica la forma de la aplicación sin ofrecer un camino a
   * ningún lado.
   */
  ready: boolean;
}

export interface NavSection {
  label: string;
  items: readonly NavItem[];
  /**
   * A qué ámbito pertenece: `"tenant"` (default) vive bajo `/tenant/[tenantId]/...`,
   * `"organization"` vive bajo `/org/[orgSlug]/...`, `"operator"` es `/nemus/...`,
   * sin tenant ni organización. `NavMain` pinta uno u otro según el `Scope` activo.
   */
  scope?: "tenant" | "organization" | "operator";
}

export const NAVIGATION: readonly NavSection[] = [
  {
    label: "Operativo",
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
      {
        href: "/clientes",
        label: "Clientes",
        description: "El directorio de RNCs y cédulas de tus receptores",
        icon: UserRound,
        minRole: ROLE.consultor,
        ready: false,
      },
    ],
  },
  {
    label: "Configuración fiscal",
    items: [
      {
        href: "/empresa",
        label: "Perfil emisor",
        description: "Los datos fiscales de tu contribuyente",
        icon: Building,
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
    ],
  },
  {
    label: "Desarrolladores & integración",
    items: [
      {
        href: "/api-keys",
        label: "API Keys",
        description: "Las llaves con las que tu ERP emite contra NovaFE",
        icon: Key,
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
        href: "/auditoria",
        label: "Logs de auditoría",
        description: "Quién hizo qué, y cuándo, sobre este contribuyente",
        icon: ScrollText,
        minRole: ROLE.admin_tenant,
        maxRole: ROLE.admin_tenant,
        ready: true,
      },
    ],
  },
  {
    label: "Ajustes",
    items: [
      {
        href: "/configuracion",
        label: "General",
        description: "Los ajustes de tu facturación",
        icon: Settings2,
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
        // «pronto». Cámbialo a `true` cuando exista `app/tenant/[tenantId]/usuarios/page.tsx`.
        ready: false,
      },
    ],
  },
  {
    // El sidebar de `/org/[orgSlug]/...` — separado del de tenant: acá no hay
    // rango de plataforma que valga (`minRole` se deja en el piso a
    // propósito, ver el comentario de `NavItem.minRole`), la autorización
    // real es el rol de organización que cada pantalla resuelve con
    // `org.role` (`owner`/`admin`/`member`, de `/users/me`).
    label: "Organización",
    scope: "organization",
    items: [
      {
        href: "/",
        label: "Tenants",
        description: "Los RNCs de esta organización",
        icon: Building2,
        minRole: ROLE.consultor,
        ready: true,
      },
      {
        href: "/miembros",
        label: "Equipo",
        description: "Quién entra a esta organización y con qué rol",
        icon: UsersRound,
        minRole: ROLE.consultor,
        ready: true,
      },
      {
        href: "/plan",
        label: "Plan",
        // A propósito no dice "facturación": en un sistema de facturación
        // electrónica esa palabra se lee como "mis e-CF", no como "lo que le
        // pago a Nemus". "Plan" es corto y no compite con eso.
        description: "Tu plan de NovaFE, consumo y método de pago",
        icon: CreditCard,
        minRole: ROLE.consultor,
        ready: true,
      },
      {
        href: "/configuracion",
        label: "Ajustes",
        description: "El nombre y los datos generales de la organización",
        icon: Settings2,
        minRole: ROLE.consultor,
        ready: false,
      },
    ],
  },
  {
    // Solo Nemus Admin: la config transversal de la plataforma para todos los
    // contribuyentes, no la de uno solo.
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
        href: "/nemus/organizaciones",
        label: "Organizaciones",
        description:
          "Cuentas pagadoras: plan, estado y los tenants que agrupan",
        icon: Boxes,
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
        description: "Los operadores del SaaS y sus roles",
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
 * El contexto activo: qué contribuyente, qué organización, o ninguno
 * (operador). Es la fuente que decide qué sección de `NAVIGATION` se pinta y
 * cómo se arman los `href` — siempre construido a partir de la URL
 * (`useParams`/`params`), nunca de una cookie ni de estado global: dos
 * pestañas en dos contextos distintos no deben pisarse.
 */
export type Scope =
  | { kind: "tenant"; tenantId: string }
  | { kind: "organization"; orgSlug: string }
  | { kind: "operator" };

/**
 * La ruta real de un `NavItem` bajo un tenant.
 *
 * `href` en `NAVIGATION` es relativo al tenant activo (`/`, `/comprobantes`,
 * `/empresa`…) — salvo `/nemus/...`, que es de operador y no lleva tenant.
 */
export function tenantHref(tenantId: string, href: string): string {
  if (href.startsWith("/nemus")) return href;

  return href === "/" ? `/tenant/${tenantId}` : `/tenant/${tenantId}${href}`;
}

/** La ruta real de un `NavItem` bajo una organización. */
export function orgHref(orgSlug: string, href: string): string {
  return href === "/" ? `/org/${orgSlug}` : `/org/${orgSlug}${href}`;
}

/** La ruta real de un `NavItem`, según el `Scope` activo. */
export function scopedHref(scope: Scope, href: string): string {
  if (href.startsWith("/nemus")) return href;

  switch (scope.kind) {
    case "tenant":
      return tenantHref(scope.tenantId, href);
    case "organization":
      return orgHref(scope.orgSlug, href);
    case "operator":
      return href;
  }
}

/** Quita el prefijo `/tenant/[id]` u `/org/[slug]` de un pathname, si lo tiene. */
function stripScopePrefix(pathname: string): string {
  const sinPrefijo = pathname.replace(/^\/(tenant|org)\/[^/]+/, "");
  return sinPrefijo === "" ? "/" : sinPrefijo;
}

/** Los items de una sección que un rango de rol de plataforma puede ver. Mismo filtro que usan `NavMain` y los switchers. */
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

/** El ámbito de una ruta absoluta, a partir de su prefijo. */
function scopeKindOf(pathname: string): NonNullable<NavSection["scope"]> {
  if (pathname.startsWith("/org/")) return "organization";
  if (pathname.startsWith("/nemus")) return "operator";
  return "tenant";
}

/**
 * El destino al que corresponde una ruta.
 *
 * Primero se filtra por **ámbito** (tenant/organización/operador, del prefijo de la
 * ruta): `/` es "Inicio" bajo un tenant pero "Tenants" bajo una organización, y sin este
 * filtro el orden de declaración en `NAVIGATION` decidiría cuál gana. Dentro del ámbito,
 * coincide por prefijo y se queda con **el más largo**, para que el detalle de algo
 * —`/usuarios/42`— siga resolviendo a su sección en vez de a la raíz.
 */
export function findNavItem(pathname: string): NavItem | undefined {
  const scope = scopeKindOf(pathname);
  const normalizado = stripScopePrefix(pathname);

  const candidatos = NAVIGATION.filter(
    (section) => (section.scope ?? "tenant") === scope,
  ).flatMap((section) => section.items);

  let mejor: NavItem | undefined;

  for (const item of candidatos) {
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
