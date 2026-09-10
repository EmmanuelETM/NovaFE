import { ROLE_LABELS, type RoleName } from "@/features/auth/roles";

/** Los roles que puede tener un usuario de contribuyente (todos menos `admin_sistema`). */
export const TENANT_ROLES: readonly RoleName[] = [
  "admin_tenant",
  "emisor",
  "consultor",
];

export const TENANT_ROLE_OPTIONS = TENANT_ROLES.map((role) => ({
  value: role,
  label: ROLE_LABELS[role],
}));
