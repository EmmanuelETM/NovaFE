"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { api } from "@/lib/api/client";
import { ApiError } from "@/lib/api/problem";
import { queryKeys } from "@/lib/api/query-keys";

import type { Certificate } from "./types";

/** El mensaje ante un error de mutación. Un 400 trae el detalle en `fieldErrors`. */
export function tenantConfigErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.isValidation) {
      const first = Object.values(error.fieldErrors).at(0);
      if (first !== undefined) return first;
    }
    return error.message;
  }
  return "No se pudo completar la acción.";
}

/** Los certificados del contribuyente actual (self-service, sin `tenantId`). */
export function useCertificates() {
  return useQuery({
    queryKey: queryKeys.myCertificates.list(),
    queryFn: () => api.get<Certificate[]>("/certificates"),
  });
}

export interface UploadCertificateInput {
  file: File;
  password: string;
  environment: string;
}

export function useUploadCertificate() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ file, password, environment }: UploadCertificateInput) => {
      const form = new FormData();
      form.set("file", file);
      form.set("password", password);
      form.set("environment", environment);

      return api.postForm<{ id: string }>("/certificates", form);
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.myCertificates.all,
      });
      toast.success("Certificado cargado");
    },
    onError: (error) => toast.error(tenantConfigErrorMessage(error)),
  });
}

export function useRevokeCertificate() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => api.post<void>(`/certificates/${id}/revoke`),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.myCertificates.all,
      });
      toast.success("Certificado revocado");
    },
    onError: (error) => toast.error(tenantConfigErrorMessage(error)),
  });
}
