import type { ReactNode } from "react";
import type { CellData, RowData, TableFeatures } from "@tanstack/react-table";

/**
 * La forma que devuelven todos los listados de la API. Se declara aquí y no se genera del
 * OpenAPI porque el genérico se pierde en la traducción: el schema produce un tipo por
 * endpoint.
 */
export interface PagedResult<TData> {
  items: TData[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

/** Una opción de un filtro de la barra de herramientas. */
export interface DataTableFilterOption {
  /** Lo que se manda a la API. */
  value: string;
  /** Lo que ve la persona, en español. */
  label: string;
}

/**
 * Un filtro del popover.
 *
 * La `key` es a la vez el nombre del parámetro en la URL y el de la consulta a la API, así
 * que coincide con el del endpoint: `categoryId`, `roleId`, `movementTypeId`.
 */
export interface DataTableFilter {
  key: string;
  label: string;
  options: DataTableFilterOption[];
}

/**
 * El estado del listado. **Vive en la URL**, no en el componente.
 *
 * Que viva ahí da tres cosas gratis: sobrevive a un refresco, el botón «atrás» funciona, y
 * un enlace a «el inventario filtrado por papel, página 3» se puede pegar en un chat. Es
 * lo que separa un listado que se siente como una aplicación de uno que se siente como un
 * formulario.
 */
export interface DataTableSearchState {
  /** Empieza en **1**, como la API. La tabla usa base 0 y la conversión vive en un solo sitio. */
  page: number;
  pageSize: number;
  /** Texto libre. Va al parámetro `filter` de la API. */
  search: string;
  /**
   * Campo y dirección, como `name` o `-name`.
   *
   * El nombre coincide con el `id` de la columna **y** con el campo que declara la API, así
   * que el estado de orden de la tabla viaja tal cual. Una columna ordenable lleva su `id`
   * puesto a propósito: sin él, el id sería el nombre de la propiedad del DTO y no el campo
   * que el endpoint acepta.
   */
  sort: string | null;
  /** Filtros por clave. Un valor null es «sin filtrar». */
  filters: Record<string, string | null>;
}

/** Lo que se muestra cuando no hay filas. Un listado en blanco nunca es aceptable. */
export interface DataTableEmptyState {
  title: string;
  description?: string;
  /** La acción que resuelve el vacío: «Crear el primer producto». */
  action?: ReactNode;
}

/**
 * Metadatos de columna del proyecto.
 *
 * `ColumnMeta` es una interfaz vacía que la librería deja abierta a propósito para esto.
 * Se amplía aquí y no se pasa por props porque la etiqueta y la alineación pertenecen a la
 * definición de la columna: separarlas obligaría a mantener dos listas en paralelo.
 */
/*
 * Los parámetros de tipo van sin usar y con SUS NOMBRES ORIGINALES a propósito:
 * TypeScript solo fusiona declaraciones de interfaz si la lista de parámetros coincide
 * exactamente, nombres incluidos. Renombrarlos a `_TFeatures` para contentar al lint
 * rompe el merge con un «All declarations of ColumnMeta must have identical type
 * parameters». Se silencia la regla en el bloque, que es lo correcto aquí.
 */
/* eslint-disable @typescript-eslint/no-unused-vars */
declare module "@tanstack/table-core" {
  interface ColumnMeta<
    in out TFeatures extends TableFeatures,
    in out TData extends RowData,
    TValue extends CellData = CellData,
  > {
    /** Nombre legible, para el menú de columnas. Sin él se usa el id. */
    label?: string;
    /** Alineación de la celda. Los importes van a la derecha, con cifras tabulares. */
    align?: "left" | "center" | "right";
  }
}
/* eslint-enable @typescript-eslint/no-unused-vars */
