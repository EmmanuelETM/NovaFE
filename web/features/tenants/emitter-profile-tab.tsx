"use client";

import { useEffect } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useController, useForm } from "react-hook-form";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { applyFieldErrors } from "@/lib/api/form-errors";
import { ApiError } from "@/lib/api/problem";
import { selectItems } from "@/lib/select-items";

import { ENVIRONMENT_OPTIONS } from "./options";
import {
  tenantErrorMessage,
  useEmitterProfile,
  useSetEmitterProfile,
} from "./use-tenants";

const schema = z.object({
  address: z.string().min(1, "La dirección del emisor es obligatoria."),
  municipality: z.string(),
  province: z.string(),
  phones: z.string(),
  email: z.string(),
  economicActivity: z.string(),
  defaultEnvironment: z
    .string()
    .min(1, "El ambiente por defecto es obligatorio."),
});

type Values = z.infer<typeof schema>;

const EMPTY_VALUES: Values = {
  address: "",
  municipality: "",
  province: "",
  phones: "",
  email: "",
  economicActivity: "",
  defaultEnvironment: "Test",
};

export function EmitterProfileTab({ tenantId }: { tenantId: string }) {
  const { data: profile, isPending, error } = useEmitterProfile(tenantId);
  const setEmitterProfile = useSetEmitterProfile();

  const notConfigured = error instanceof ApiError && error.status === 404;

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: EMPTY_VALUES,
  });
  const environment = useController({
    control: form.control,
    name: "defaultEnvironment",
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
      defaultEnvironment: profile.defaultEnvironment,
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [profile]);

  const submit = form.handleSubmit(async (values) => {
    try {
      await setEmitterProfile.mutateAsync({
        tenantId,
        address: values.address.trim(),
        municipality: values.municipality.trim() || null,
        province: values.province.trim() || null,
        phones: values.phones
          .split(",")
          .map((phone) => phone.trim())
          .filter(Boolean),
        email: values.email.trim() || null,
        economicActivity: values.economicActivity.trim() || null,
        defaultEnvironment: values.defaultEnvironment,
      });
    } catch (error) {
      if (!applyFieldErrors(error, form.setError)) {
        form.setError("address", { message: tenantErrorMessage(error) });
      }
    }
  });

  if (isPending) {
    return <p className="text-muted-foreground text-sm">Cargando…</p>;
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Perfil fiscal del emisor</CardTitle>
      </CardHeader>
      <CardContent>
        {notConfigured && (
          <p className="text-muted-foreground mb-4 text-sm">
            Este contribuyente todavía no tiene perfil de emisor. Complétalo
            para poder acuñar su primera API key.
          </p>
        )}

        <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
          <Field>
            <FieldLabel htmlFor="address">Dirección</FieldLabel>
            <Input id="address" {...form.register("address")} />
            <FieldError errors={[form.formState.errors.address]} />
          </Field>

          <Field>
            <FieldLabel htmlFor="municipality">
              Municipio (código Tabla III, opcional)
            </FieldLabel>
            <Input id="municipality" {...form.register("municipality")} />
            <FieldError errors={[form.formState.errors.municipality]} />
          </Field>

          <Field>
            <FieldLabel htmlFor="province">
              Provincia (código Tabla III, opcional)
            </FieldLabel>
            <Input id="province" {...form.register("province")} />
            <FieldError errors={[form.formState.errors.province]} />
          </Field>

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
            <Input
              id="economicActivity"
              {...form.register("economicActivity")}
            />
            <FieldError errors={[form.formState.errors.economicActivity]} />
          </Field>

          <Field>
            <FieldLabel htmlFor="defaultEnvironment">
              Ambiente por defecto
            </FieldLabel>
            <Select
              items={selectItems(ENVIRONMENT_OPTIONS)}
              value={environment.field.value}
              onValueChange={(next) => environment.field.onChange(String(next))}
            >
              <SelectTrigger id="defaultEnvironment" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {ENVIRONMENT_OPTIONS.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <FieldError errors={[form.formState.errors.defaultEnvironment]} />
          </Field>

          <div className="flex justify-end">
            <Button type="submit" disabled={form.formState.isSubmitting}>
              Guardar
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
