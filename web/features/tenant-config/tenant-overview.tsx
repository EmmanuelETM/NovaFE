"use client";

import { StatTile } from "@/components/shared/stat-tile";
import { useCertificates } from "./use-certificates";
import { useEmitterProfile } from "./use-emitter-profile";
import { useEcfList } from "@/features/ecf/use-ecf";
import { formatCalendarDate, formatDate } from "@/lib/format";

/** Un certificado que vence en menos de esto se marca en amarillo. */
const CERT_EXPIRY_WARNING_DAYS = 30;

function daysUntil(iso: string): number {
  const ms = new Date(iso).getTime() - Date.now();
  return Math.ceil(ms / (1000 * 60 * 60 * 24));
}

/**
 * El resumen operativo de `/inicio`: cuántos e-CF lleva emitidos, el último,
 * y el estado del certificado del ambiente activo. Vivía como badges sueltos
 * en el grid de tenants de la organización — se movió acá porque es
 * información del contribuyente puntual, no algo que se compare de un
 * vistazo entre los tenants de una organización.
 */
export function TenantOverview() {
  const { data: profile } = useEmitterProfile();
  const { data: certificates, isPending: certificatesPending } =
    useCertificates();
  const { data: ecf, isPending: ecfPending } = useEcfList({
    page: 1,
    pageSize: 1,
    search: "",
    sort: null,
    filters: {},
  });

  const activeCertificate = certificates?.find(
    (cert) =>
      cert.status === "Active" &&
      (!profile || cert.environment === profile.defaultEnvironment),
  );

  const lastEcf = ecf?.items[0];
  const totalCount = ecf ? Number(ecf.totalCount) : undefined;

  const certDays = activeCertificate
    ? daysUntil(activeCertificate.validTo)
    : null;

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <StatTile
        label="Comprobantes emitidos"
        value={totalCount !== undefined ? String(totalCount) : ""}
        loading={ecfPending}
      />

      <StatTile
        label="Último e-CF"
        value={lastEcf ? lastEcf.encf : "—"}
        hint={
          lastEcf
            ? formatCalendarDate(lastEcf.issueDate)
            : "Sin emisiones todavía"
        }
        loading={ecfPending}
      />

      <StatTile
        label="Certificado"
        value={
          activeCertificate
            ? formatDate(activeCertificate.validTo)
            : "Sin certificado"
        }
        hint={
          activeCertificate
            ? certDays !== null && certDays < 0
              ? `Vencido hace ${Math.abs(certDays)} días`
              : `Vence en ${certDays} días`
            : "Cargá uno en Certificados"
        }
        highlight={certDays !== null && certDays < CERT_EXPIRY_WARNING_DAYS}
        loading={certificatesPending}
      />
    </div>
  );
}
