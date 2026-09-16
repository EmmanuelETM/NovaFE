"use client";

import {
  useController,
  type Control,
  type FieldValues,
  type Path,
} from "react-hook-form";

import { Combobox } from "@/components/shared/combobox";
import { Field, FieldLabel } from "@/components/ui/field";
import { PROVINCIAS } from "@/lib/catalog/provincias-municipios";

interface ProvinciaMunicipioFieldsProps<TFieldValues extends FieldValues> {
  control: Control<TFieldValues>;
  /** Nombre del campo del form que guarda el código de provincia (6 dígitos). */
  provinceName: Path<TFieldValues>;
  /** Nombre del campo del form que guarda el código de municipio/distrito (6 dígitos). */
  municipalityName: Path<TFieldValues>;
}

/**
 * Provincia + Municipio de la Tabla III de la DGII, como dos combobox en
 * cascada: el municipio solo lista lo que pertenece a la provincia elegida
 * (municipios y sus distritos municipales juntos, en una sola lista
 * buscable — la DGII valida `<Municipio>` contra cualquiera de los dos
 * niveles). Un solo lugar para esta lógica: la usan tanto el alta guiada de
 * un contribuyente como la pestaña de perfil de uno ya existente.
 */
export function ProvinciaMunicipioFields<TFieldValues extends FieldValues>({
  control,
  provinceName,
  municipalityName,
}: ProvinciaMunicipioFieldsProps<TFieldValues>) {
  const province = useController({ control, name: provinceName });
  const municipality = useController({ control, name: municipalityName });

  const provinceValue = String(province.field.value ?? "");
  const municipalityValue = String(municipality.field.value ?? "");

  const selectedProvincia = PROVINCIAS.find((p) => p.code === provinceValue);

  const municipioOptions = selectedProvincia
    ? selectedProvincia.municipios.flatMap((municipio) => [
        municipio,
        ...municipio.distritos,
      ])
    : [];

  return (
    <>
      <Field>
        <FieldLabel>Provincia (opcional)</FieldLabel>
        <Combobox
          options={PROVINCIAS}
          value={provinceValue}
          onChange={(next) => {
            province.field.onChange(next);
            // Un municipio de la provincia anterior ya no es válido acá.
            municipality.field.onChange("");
          }}
          getValue={(p) => p.code}
          getLabel={(p) => p.name}
          placeholder="Elegí una provincia…"
          searchPlaceholder="Buscar provincia…"
          emptyText="Sin resultados."
        />
      </Field>

      <Field>
        <FieldLabel>Municipio (opcional)</FieldLabel>
        <Combobox
          options={municipioOptions}
          value={municipalityValue}
          onChange={(next) => municipality.field.onChange(next)}
          getValue={(m) => m.code}
          getLabel={(m) => m.name}
          placeholder={
            selectedProvincia
              ? "Elegí un municipio…"
              : "Elegí una provincia primero"
          }
          searchPlaceholder="Buscar municipio…"
          emptyText="Sin resultados."
          disabled={!selectedProvincia}
        />
      </Field>
    </>
  );
}
