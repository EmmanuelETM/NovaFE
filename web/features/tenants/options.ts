/** Los 5 niveles comerciales (`TenantPlan`), en el orden que conviene mostrar. */
export const PLAN_OPTIONS = [
  { value: "Developer", label: "Developer" },
  { value: "Starter", label: "Emprendedor" },
  { value: "Business", label: "Negocio" },
  { value: "Corporate", label: "Corporativo" },
  { value: "Enterprise", label: "Empresarial" },
];

/** Los 3 ambientes de la DGII (`DgiiEnvironment`). */
export const ENVIRONMENT_OPTIONS = [
  { value: "Test", label: "Test (TestECF)" },
  { value: "Cert", label: "Certificación (CerteCF)" },
  { value: "Production", label: "Producción (eCF)" },
];

/** Los 10 tipos de e-CF (`EcfType`), con su código y nombre DGII. */
export const ECF_TYPE_OPTIONS = [
  { value: "31", label: "31 — Crédito Fiscal" },
  { value: "32", label: "32 — Consumo" },
  { value: "33", label: "33 — Nota de Débito" },
  { value: "34", label: "34 — Nota de Crédito" },
  { value: "41", label: "41 — Compras" },
  { value: "43", label: "43 — Gastos Menores" },
  { value: "44", label: "44 — Regímenes Especiales" },
  { value: "45", label: "45 — Gubernamental" },
  { value: "46", label: "46 — Exportaciones" },
  { value: "47", label: "47 — Pagos al Exterior" },
];
