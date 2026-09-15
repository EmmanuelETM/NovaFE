import type { components } from "@/lib/api/schema";

/** Un contribuyente (`TenantDto`). */
export type Tenant = components["schemas"]["TenantDto"];

/** Fila del listado de contribuyentes (`TenantSummaryDto`). */
export type TenantSummary = components["schemas"]["TenantSummaryDto"];

/** El perfil fiscal del emisor (`EmitterProfileDto`). */
export type EmitterProfile = components["schemas"]["EmitterProfileDto"];

/** Un certificado digital cargado para el contribuyente (`CertificateDto`). */
export type Certificate = components["schemas"]["CertificateDto"];

/** Un rango de e-NCF registrado (`NcfSequenceDto`). */
export type NcfSequence = components["schemas"]["NcfSequenceDto"];

/** Una API key acuñada (`ApiKeyDto`, sin el token). */
export type ApiKey = components["schemas"]["ApiKeyDto"];

/** La respuesta al acuñar: la key **y** el token en claro (única vez que se ve). */
export type ApiKeyCreated = components["schemas"]["ApiKeyCreatedDto"];

export type TenantPage = components["schemas"]["PagedResultOfTenantSummaryDto"];
