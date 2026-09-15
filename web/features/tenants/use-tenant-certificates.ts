"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { api } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/query-keys";

import { tenantErrorMessage } from "./use-tenants";
import type { Certificate } from "./types";

/** Los certificados de un contribuyente, cargados por el operador. */
export function useTenantCertificates(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.tenants.certificates(tenantId),
    queryFn: () => api.get<Certificate[]>(`/tenants/${tenantId}/certificates`),
    enabled: tenantId !== "",
  });
}

export interface UploadCertificateInput {
  tenantId: string;
  file: File;
  password: string;
  environment: string;
}

export function useUploadCertificate() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      tenantId,
      file,
      password,
      environment,
    }: UploadCertificateInput) => {
      const form = new FormData();
      form.set("file", file);
      form.set("password", password);
      form.set("environment", environment);

      return api.postForm<{ id: string }>(
        `/tenants/${tenantId}/certificates`,
        form,
      );
    },
    onSuccess: (_data, { tenantId }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.tenants.certificates(tenantId),
      });
      toast.success("Certificado cargado");
    },
    onError: (error) => toast.error(tenantErrorMessage(error)),
  });
}

export function useRevokeCertificate() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ tenantId, id }: { tenantId: string; id: string }) =>
      api.post<void>(`/tenants/${tenantId}/certificates/${id}/revoke`),
    onSuccess: (_data, { tenantId }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.tenants.certificates(tenantId),
      });
      toast.success("Certificado revocado");
    },
    onError: (error) => toast.error(tenantErrorMessage(error)),
  });
}
