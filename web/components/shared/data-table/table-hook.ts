import {
  columnVisibilityFeature,
  createTableHook,
  rowPaginationFeature,
  rowSelectionFeature,
  rowSortingFeature,
  tableFeatures,
} from "@tanstack/react-table";

/**
 * La infraestructura de tablas del proyecto, declarada una sola vez.
 *
 * `createTableHook` liga el conjunto de features y los defaults, de modo que cada tabla
 * solo aporta sus datos, sus columnas y su estado. Es lo que evita que la tercera pantalla
 * registre otras features y se comporte distinto sin que nadie lo note.
 *
 * **En v9 las features se registran explícitamente.** Es la diferencia principal con la v8,
 * donde venían todas: si una API de estado no existe, casi siempre falta importar su
 * feature, no es un problema de tipos.
 */
export const { createAppColumnHelper, useAppTable, useTableContext } =
  createTableHook({
    features: tableFeatures({
      rowSortingFeature,
      rowPaginationFeature,
      rowSelectionFeature,
      columnVisibilityFeature,
    }),

    /**
     * **El servidor procesa todo.** Los endpoints devuelven `PagedResult<T>`, así que la
     * tabla solo coordina estado y pinta: no filtra, no ordena y no pagina en memoria.
     *
     * Sin estas banderas, la tabla intentaría paginar los veinte registros que ya vinieron
     * paginados y el listado mentiría — mostraría «página 1 de 1» sobre 500 filas.
     */
    manualPagination: true,
    manualSorting: true,

    /**
     * Ninguna columna es ordenable por defecto: cada una opta con `enableSorting: true`.
     *
     * Es al revés de lo natural, y a propósito. La API solo acepta ordenar por una lista
     * corta de campos, así que una columna ordenable de más produce un control que no hace
     * nada — y un control que no responde es peor que uno que no está.
     *
     * El `id` de una columna ordenable tiene que ser el nombre del campo **que la API
     * declara**: es lo que viaja como parámetro `sort`.
     */
    enableSorting: false,
  });

/**
 * No se registra `columnFilteringFeature` a propósito, y por eso tampoco existe
 * `manualFiltering`: el filtrado no pasa por la tabla. Vive en la URL y viaja a la API como
 * parámetros de consulta, que es donde puede filtrar de verdad sobre las 500 filas y no
 * sobre las 20 que ya llegaron.
 *
 * En v9 una API de estado que no existe casi siempre significa que falta importar su
 * feature. Aquí significa lo contrario: que sobra.
 */
