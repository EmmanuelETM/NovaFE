import { Crown, ShieldCheck, UserRound } from "lucide-react";

import type { components } from "@/lib/api/schema";

/** Una organización del usuario (`UserOrganizationDto`, de `/users/me`). */
export type UserOrganization = components["schemas"]["UserOrganizationDto"];

/** Una organización, vista de operador (`OrganizationDto`, `GET /organizations/{id}`). */
export type Organization = components["schemas"]["OrganizationDto"];

/** Fila del listado de operador (`OrganizationSummaryDto`). */
export type OrganizationSummary =
  components["schemas"]["OrganizationSummaryDto"];

export type OrganizationPage =
  components["schemas"]["PagedResultOfOrganizationSummaryDto"];

/** Un miembro de la organización (`OrganizationMemberDto`). */
export type OrganizationMember = components["schemas"]["OrganizationMemberDto"];

/** Fila del grid de tenants de una organización (`OrganizationTenantSummaryDto`). */
export type OrganizationTenantSummary =
  components["schemas"]["OrganizationTenantSummaryDto"];

export type OrganizationTenantPage =
  components["schemas"]["PagedResultOfOrganizationTenantSummaryDto"];

/** Roles de organización, en el orden en que se ofrecen al invitar. */
export const ORGANIZATION_ROLE_OPTIONS = [
  { value: "member", label: "Member", icon: UserRound },
  { value: "admin", label: "Admin", icon: ShieldCheck },
  { value: "owner", label: "Owner", icon: Crown },
] as const;

export function organizationRoleLabel(role: string): string {
  return (
    ORGANIZATION_ROLE_OPTIONS.find((option) => option.value === role)?.label ??
    role
  );
}
