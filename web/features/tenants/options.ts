/** Los 5 niveles comerciales (`TenantPlan`/`OrganizationPlan`), en el orden que conviene mostrar. */
export const PLAN_OPTIONS = [
  { value: "Developer", label: "Developer" },
  { value: "Starter", label: "Emprendedor" },
  { value: "Business", label: "Negocio" },
  { value: "Corporate", label: "Corporativo" },
  { value: "Enterprise", label: "Empresarial" },
];

export function planLabel(value: string): string {
  return PLAN_OPTIONS.find((option) => option.value === value)?.label ?? value;
}

/** Los 3 ambientes de la DGII (`DgiiEnvironment`). */
export const ENVIRONMENT_OPTIONS = [
  { value: "Test", label: "Test (TestECF)" },
  { value: "Cert", label: "Certificación (CerteCF)" },
  { value: "Production", label: "Producción (eCF)" },
];

export function environmentLabel(value: string): string {
  return (
    ENVIRONMENT_OPTIONS.find((option) => option.value === value)?.label ?? value
  );
}

/**
 * Color por ambiente — mismo criterio en todos lados que muestran un badge
 * de ambiente (`TenantSwitcher`, el grid de tenants de una organización):
 * Test en ámbar, Cert en celeste, Producción en verde.
 */
export const ENVIRONMENT_COLOR: Record<string, string> = {
  Test: "border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-400",
  Cert: "border-sky-500/30 bg-sky-500/10 text-sky-700 dark:text-sky-400",
  Production:
    "border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400",
};

/** Los 10 tipos de e-CF (`EcfType`), con su código y nombre DGII. */
export const ECF_TYPE_OPTIONS = [
  { value: "31", label: "31 - Crédito Fiscal" },
  { value: "32", label: "32 - Consumo" },
  { value: "33", label: "33 - Nota de Débito" },
  { value: "34", label: "34 - Nota de Crédito" },
  { value: "41", label: "41 - Compras" },
  { value: "43", label: "43 - Gastos Menores" },
  { value: "44", label: "44 - Regímenes Especiales" },
  { value: "45", label: "45 - Gubernamental" },
  { value: "46", label: "46 - Exportaciones" },
  { value: "47", label: "47 - Pagos al Exterior" },
];
