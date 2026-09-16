"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
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
import { selectItems } from "@/lib/select-items";

import { ENVIRONMENT_OPTIONS, PLAN_OPTIONS } from "./options";
import { ProvinciaMunicipioFields } from "./provincia-municipio-fields";
import {
  tenantErrorMessage,
  useRegisterTenant,
  useSetEmitterProfile,
} from "./use-tenants";

const tenantSchema = z.object({
  rnc: z
    .string()
    .min(1, "El RNC es obligatorio.")
    .regex(
      /^\d{9,11}$/,
      "El RNC debe tener entre 9 y 11 dígitos, sin separadores.",
    ),
  legalName: z.string().min(1, "La razón social es obligatoria."),
  tradeName: z.string(),
  plan: z.string().min(1, "El plan es obligatorio."),
});

type TenantValues = z.infer<typeof tenantSchema>;

const profileSchema = z.object({
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

type ProfileValues = z.infer<typeof profileSchema>;

/**
 * Alta guiada de un contribuyente, en dos pasos. Estado local, no la URL: un
 * alta a medias no necesita sobrevivir un refresco.
 */
export function RegisterTenantWizard() {
  const router = useRouter();
  const [tenantId, setTenantId] = useState<string | null>(null);
  const registerTenant = useRegisterTenant();
  const setEmitterProfile = useSetEmitterProfile();

  const tenantForm = useForm<TenantValues>({
    resolver: zodResolver(tenantSchema),
    defaultValues: { rnc: "", legalName: "", tradeName: "", plan: "Business" },
  });
  const plan = useController({ control: tenantForm.control, name: "plan" });

  const profileForm = useForm<ProfileValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      address: "",
      municipality: "",
      province: "",
      phones: "",
      email: "",
      economicActivity: "",
      defaultEnvironment: "Test",
    },
  });
  const environment = useController({
    control: profileForm.control,
    name: "defaultEnvironment",
  });

  const submitTenant = tenantForm.handleSubmit(async (values) => {
    try {
      const created = await registerTenant.mutateAsync({
        rnc: values.rnc.trim(),
        legalName: values.legalName.trim(),
        tradeName: values.tradeName.trim() || undefined,
        plan: values.plan,
      });
      setTenantId(created.id);
    } catch (error) {
      if (!applyFieldErrors(error, tenantForm.setError)) {
        tenantForm.setError("rnc", { message: tenantErrorMessage(error) });
      }
    }
  });

  const submitProfile = profileForm.handleSubmit(async (values) => {
    if (!tenantId) return;

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
      router.push(`/nemus/tenants/${tenantId}`);
    } catch (error) {
      if (!applyFieldErrors(error, profileForm.setError)) {
        profileForm.setError("address", {
          message: tenantErrorMessage(error),
        });
      }
    }
  });

  if (tenantId === null) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>1. Datos del contribuyente</CardTitle>
        </CardHeader>
        <CardContent>
          <form
            onSubmit={submitTenant}
            className="flex flex-col gap-4"
            noValidate
          >
            <Field>
              <FieldLabel htmlFor="rnc">RNC</FieldLabel>
              <Input
                id="rnc"
                autoComplete="off"
                {...tenantForm.register("rnc")}
              />
              <FieldError errors={[tenantForm.formState.errors.rnc]} />
            </Field>

            <Field>
              <FieldLabel htmlFor="legalName">Razón social</FieldLabel>
              <Input id="legalName" {...tenantForm.register("legalName")} />
              <FieldError errors={[tenantForm.formState.errors.legalName]} />
            </Field>

            <Field>
              <FieldLabel htmlFor="tradeName">
                Nombre comercial (opcional)
              </FieldLabel>
              <Input id="tradeName" {...tenantForm.register("tradeName")} />
              <FieldError errors={[tenantForm.formState.errors.tradeName]} />
            </Field>

            <Field>
              <FieldLabel htmlFor="plan">Plan</FieldLabel>
              <Select
                items={selectItems(PLAN_OPTIONS)}
                value={plan.field.value}
                onValueChange={(next) => plan.field.onChange(String(next))}
              >
                <SelectTrigger id="plan" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PLAN_OPTIONS.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <FieldError errors={[tenantForm.formState.errors.plan]} />
            </Field>

            <div className="flex justify-end">
              <Button
                type="submit"
                disabled={tenantForm.formState.isSubmitting}
              >
                Continuar
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>2. Perfil fiscal del emisor</CardTitle>
      </CardHeader>
      <CardContent>
        <form
          onSubmit={submitProfile}
          className="flex flex-col gap-4"
          noValidate
        >
          <Field>
            <FieldLabel htmlFor="address">Dirección</FieldLabel>
            <Input id="address" {...profileForm.register("address")} />
            <FieldError errors={[profileForm.formState.errors.address]} />
          </Field>

          <ProvinciaMunicipioFields
            control={profileForm.control}
            provinceName="province"
            municipalityName="municipality"
          />

          <Field>
            <FieldLabel htmlFor="phones">
              Teléfonos (hasta 3, separados por coma)
            </FieldLabel>
            <Input id="phones" {...profileForm.register("phones")} />
            <FieldError errors={[profileForm.formState.errors.phones]} />
          </Field>

          <Field>
            <FieldLabel htmlFor="email">Correo (opcional)</FieldLabel>
            <Input id="email" type="email" {...profileForm.register("email")} />
            <FieldError errors={[profileForm.formState.errors.email]} />
          </Field>

          <Field>
            <FieldLabel htmlFor="economicActivity">
              Actividad económica (opcional)
            </FieldLabel>
            <Input
              id="economicActivity"
              {...profileForm.register("economicActivity")}
            />
            <FieldError
              errors={[profileForm.formState.errors.economicActivity]}
            />
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
            <FieldError
              errors={[profileForm.formState.errors.defaultEnvironment]}
            />
          </Field>

          <div className="flex justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => router.push(`/nemus/tenants/${tenantId}`)}
            >
              Completar después
            </Button>
            <Button type="submit" disabled={profileForm.formState.isSubmitting}>
              Terminar alta
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
