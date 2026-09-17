import type { components } from "@/lib/api/schema";

/** Una organización del usuario (`UserOrganizationDto`, de `/users/me`). */
export type UserOrganization = components["schemas"]["UserOrganizationDto"];

/** Un miembro de la organización (`OrganizationMemberDto`). */
export type OrganizationMember = components["schemas"]["OrganizationMemberDto"];

/** Fila del grid de tenants de una organización (`OrganizationTenantSummaryDto`). */
export type OrganizationTenantSummary =
  components["schemas"]["OrganizationTenantSummaryDto"];

export type OrganizationTenantPage =
  components["schemas"]["PagedResultOfOrganizationTenantSummaryDto"];

/** Roles de organización, en el orden en que se ofrecen al invitar. */
export const ORGANIZATION_ROLE_OPTIONS = [
  { value: "member", label: "Member" },
  { value: "admin", label: "Admin" },
  { value: "owner", label: "Owner" },
] as const;

export function organizationRoleLabel(role: string): string {
  return (
    ORGANIZATION_ROLE_OPTIONS.find((option) => option.value === role)?.label ??
    role
  );
}
