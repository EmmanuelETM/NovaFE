"use client";

import { useEffect } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { applyFieldErrors } from "@/lib/api/form-errors";
import { ApiError } from "@/lib/api/problem";
import { ENVIRONMENT_OPTIONS } from "@/features/tenants/options";
import { ProvinciaMunicipioFields } from "@/features/tenants/provincia-municipio-fields";

import { tenantConfigErrorMessage } from "./use-certificates";
import { useEmitterProfile, useSetEmitterProfile } from "./use-emitter-profile";

const schema = z.object({
  address: z.string().min(1, "La dirección del emisor es obligatoria."),
  municipality: z.string(),
  province: z.string(),
  phones: z.string(),
  email: z.string(),
  economicActivity: z.string(),
});

type Values = z.infer<typeof schema>;

const EMPTY_VALUES: Values = {
  address: "",
  municipality: "",
  province: "",
  phones: "",
  email: "",
  economicActivity: "",
};

function environmentLabel(value: string): string {
  return (
    ENVIRONMENT_OPTIONS.find((option) => option.value === value)?.label ?? value
  );
}

/**
 * Perfil fiscal del emisor del propio contribuyente (dirección, provincia/
 * municipio, teléfonos, ambiente) — antes solo lo podía cargar un operador
 * desde `/nemus/tenants/{id}`. El ambiente por defecto se ve pero no se
 * edita acá: cambiarlo exige certificado y rango de secuencia ya
 * autorizados en el ambiente destino, algo que confirma Nemus, no un
 * cambio unilateral del propio contribuyente.
 */
export function EmpresaScreen() {
  const { data: profile, isPending, error } = useEmitterProfile();
  const setEmitterProfile = useSetEmitterProfile();

  // `EmitterProfileErrors.NotConfigured` es un `Error.Validation` en el
  // backend (no `NotFound`), así que la API la manda como 400, no 404 — el
  // código del error, no el status HTTP, es lo único que la distingue de
  // cualquier otra falla (p. ej. la sesión no resuelve un tenant).
  const notConfigured =
    error instanceof ApiError &&
    "EmitterProfile.NotConfigured" in error.fieldErrors;

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: EMPTY_VALUES,
  });

  useEffect(() => {
    if (!profile) return;

    form.reset({
      address: profile.address,
      municipality: profile.municipality ?? "",
      province: profile.province ?? "",
      phones: profile.phones.join(", "),
      email: profile.email ?? "",
      economicActivity: profile.economicActivity ?? "",
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [profile]);

  const submit = form.handleSubmit(async (values) => {
    try {
      await setEmitterProfile.mutateAsync({
        address: values.address.trim(),
        municipality: values.municipality.trim() || null,
        province: values.province.trim() || null,
        phones: values.phones
          .split(",")
          .map((phone) => phone.trim())
          .filter(Boolean),
        email: values.email.trim() || null,
        economicActivity: values.economicActivity.trim() || null,
      });
    } catch (error) {
      if (!applyFieldErrors(error, form.setError)) {
        form.setError("address", { message: tenantConfigErrorMessage(error) });
      }
    }
  });

  if (isPending) {
    return <p className="text-muted-foreground text-sm">Cargando…</p>;
  }

  if (notConfigured) {
    return (
      <p className="text-muted-foreground text-sm">
        Todavía no tenés un perfil de emisor configurado. Escribile a Nemus para
        darlo de alta.
      </p>
    );
  }

  if (error) {
    return (
      <p className="text-destructive text-sm">
        {tenantConfigErrorMessage(error)}
      </p>
    );
  }

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      {profile && (
        <div className="flex items-center gap-2 text-sm">
          <span className="text-muted-foreground">Ambiente actual:</span>
          <Badge variant="outline">
            {environmentLabel(profile.defaultEnvironment)}
          </Badge>
          <span className="text-muted-foreground text-xs">
            (lo cambia Nemus, no se edita acá)
          </span>
        </div>
      )}

      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        <Field>
          <FieldLabel htmlFor="address">Dirección</FieldLabel>
          <Input id="address" {...form.register("address")} />
          <FieldError errors={[form.formState.errors.address]} />
        </Field>

        <ProvinciaMunicipioFields
          control={form.control}
          provinceName="province"
          municipalityName="municipality"
        />

        <Field>
          <FieldLabel htmlFor="phones">
            Teléfonos (hasta 3, separados por coma)
          </FieldLabel>
          <Input id="phones" {...form.register("phones")} />
          <FieldError errors={[form.formState.errors.phones]} />
        </Field>

        <Field>
          <FieldLabel htmlFor="email">Correo (opcional)</FieldLabel>
          <Input id="email" type="email" {...form.register("email")} />
          <FieldError errors={[form.formState.errors.email]} />
        </Field>

        <Field>
          <FieldLabel htmlFor="economicActivity">
            Actividad económica (opcional)
          </FieldLabel>
          <Input id="economicActivity" {...form.register("economicActivity")} />
          <FieldError errors={[form.formState.errors.economicActivity]} />
        </Field>

        <div className="flex justify-end">
          <Button type="submit" disabled={form.formState.isSubmitting}>
            Guardar
          </Button>
        </div>
      </form>
    </div>
  );
}
