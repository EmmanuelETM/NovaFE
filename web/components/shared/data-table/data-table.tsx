"use client";

import { useMemo } from "react";
import type {
  OnChangeFn,
  PaginationState,
  RowData,
  RowSelectionState,
  SortingState,
} from "@tanstack/react-table";

import { Checkbox } from "@/components/ui/checkbox";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { cn } from "@/lib/utils";

import { DataTablePagination } from "./data-table-pagination";
import {
  DataTableEmpty,
  DataTableError,
  DataTableSkeletonRows,
} from "./data-table-states";
import { DataTableToolbar } from "./data-table-toolbar";
import { createAppColumnHelper, useAppTable } from "./table-hook";
import type {
  DataTableEmptyState,
  DataTableFilter,
  DataTableSearchState,
  PagedResult,
} from "./types";

/**
 * Las columnas tal como las produce `createAppColumnHelper`. Se infiere en lugar de
 * escribirse porque el tipo lleva el conjunto de features dentro: nombrarlo a mano sería
 * repetir lo que la fábrica ya sabe.
 */
export type AppColumns<TData extends RowData> = ReturnType<
  ReturnType<typeof createAppColumnHelper<TData>>["columns"]
>;

/** Referencia estable: `?? []` crearía un arreglo nuevo en cada render. */
const EMPTY_ROWS: never[] = [];

/**
 * La columna de casillas, se antepone a las de la pantalla cuando `rowSelection` está
 * activo. Va aquí y no en cada tabla que la necesite, para que la casilla se vea y se
 * comporte igual en todas.
 *
 * `stopPropagation` en las dos porque la fila entera es clickeable cuando hay
 * `onRowClick` —abre un diálogo, por ejemplo— y marcar la casilla no debería abrirlo.
 */
function selectionColumn<TData extends RowData>() {
  return createAppColumnHelper<TData>().display({
    id: "select",
    header: ({ table }) => (
      <div
        className="flex items-center justify-center"
        onClick={(event) => event.stopPropagation()}
        onKeyDown={(event) => event.stopPropagation()}
      >
        <Checkbox
          checked={table.getIsAllPageRowsSelected()}
          indeterminate={
            table.getIsSomePageRowsSelected() &&
            !table.getIsAllPageRowsSelected()
          }
          onCheckedChange={(checked) =>
            table.toggleAllPageRowsSelected(checked)
          }
          aria-label="Seleccionar todas las filas de la página"
        />
      </div>
    ),
    cell: ({ row }) => (
      <div
        className="flex items-center justify-center"
        onClick={(event) => event.stopPropagation()}
        onKeyDown={(event) => event.stopPropagation()}
      >
        <Checkbox
          checked={row.getIsSelected()}
          onCheckedChange={(checked) => row.toggleSelected(checked)}
          aria-label="Seleccionar fila"
        />
      </div>
    ),
  });
}

export interface DataTableProps<TData extends RowData> {
  columns: AppColumns<TData>;

  /** La página tal como la devuelve la API. `undefined` mientras carga. */
  page: PagedResult<TData> | undefined;

  /** Primera carga: no hay nada que mostrar todavía. */
  isPending: boolean;

  /**
   * Recargando con datos anteriores en pantalla. Se atenúa la tabla en lugar de vaciarla:
   * con `keepPreviousData`, vaciarla en cada página se siente roto.
   */
  isFetching?: boolean;

  error?: unknown;

  state: DataTableSearchState;
  onStateChange: (patch: Partial<DataTableSearchState>) => void;

  filters?: DataTableFilter[];

  /**
   * Si la pantalla ofrece búsqueda por texto libre. **Se apaga cuando el endpoint no la
   * acepta** —el listado de ventas, por ejemplo, que se recorre por período y no por
   * texto—: un buscador que no busca es peor que no tener buscador, porque quien escribe en
   * él concluye que no hay resultados.
   */
  searchable?: boolean;
  searchPlaceholder?: string;
  emptyState?: DataTableEmptyState;

  /** Acciones de la pantalla, a la derecha de la barra: «Nuevo producto». */
  toolbarActions?: React.ReactNode;

  /** Si se pasa, las filas se vuelven interactivas — con teclado incluido. */
  onRowClick?: (row: TData) => void;

  /** Identidad estable de la fila. Por defecto, el índice. */
  getRowId?: (row: TData, index: number) => string;

  /**
   * Casillas de selección, una por fila más el «seleccionar todas» del encabezado. Se
   * activa mandando **los dos** —`rowSelection` y `onRowSelectionChange`—, controlados por
   * quien usa la tabla: la selección es intención de la pantalla (qué exportar, qué borrar
   * en lote), no estado de la tabla misma.
   *
   * Con `getRowId` puesto, la selección sobrevive al cambio de página — es un Id, no una
   * posición—, que es justo lo que hace falta para marcar filas de distintas páginas antes
   * de actuar sobre todas.
   */
  rowSelection?: RowSelectionState;
  onRowSelectionChange?: OnChangeFn<RowSelectionState>;
}

/**
 * La tabla del proyecto. **Toda tabla usa esta**; nunca `<table>` suelto.
 *
 * No es una regla estética. Paginación, orden, filtros, y los estados vacío, de carga y de
 * error son seis cosas que hay que resolver bien una vez. Escritas por pantalla, para el
 * cuarto listado ya hay cuatro comportamientos distintos — y eso es exactamente lo que se
 * nota como falta de pulido.
 *
 * Todo el procesamiento es **del servidor** (ver `table-hook.ts`): los endpoints devuelven
 * `PagedResult<T>` y la tabla solo coordina estado y pinta.
 */
export function DataTable<TData extends RowData>({
  columns,
  page,
  isPending,
  isFetching = false,
  error,
  state,
  onStateChange,
  filters = [],
  searchable = true,
  searchPlaceholder = "Buscar…",
  emptyState = { title: "Sin resultados" },
  toolbarActions,
  onRowClick,
  getRowId,
  rowSelection,
  onRowSelectionChange,
}: DataTableProps<TData>) {
  const selectionEnabled =
    rowSelection !== undefined && onRowSelectionChange !== undefined;
  // La API cuenta las páginas desde 1 y la tabla desde 0. La conversión vive aquí y solo
  // aquí: repartida por las pantallas es como aparecen los errores de una página de
  // corrimiento.
  const pagination = useMemo<PaginationState>(
    () => ({
      pageIndex: Math.max(0, state.page - 1),
      pageSize: state.pageSize,
    }),
    [state.page, state.pageSize],
  );

  const sorting = useMemo<SortingState>(() => {
    if (!state.sort) return [];

    const desc = state.sort.startsWith("-");

    return [{ id: desc ? state.sort.slice(1) : state.sort, desc }];
  }, [state.sort]);

  // Los `on*Change` reciben un valor o una función; hay que resolver los dos casos.
  const handlePaginationChange: OnChangeFn<PaginationState> = (updater) => {
    const next = typeof updater === "function" ? updater(pagination) : updater;

    onStateChange({ page: next.pageIndex + 1, pageSize: next.pageSize });
  };

  const handleSortingChange: OnChangeFn<SortingState> = (updater) => {
    const next = typeof updater === "function" ? updater(sorting) : updater;
    const first = next.at(0);

    onStateChange({
      sort: first ? `${first.desc ? "-" : ""}${first.id}` : null,
    });
  };

  // Se arma una sola vez y no en cada render: es una definición de columna, no un valor
  // que cambie con los datos.
  const tableColumns = useMemo(
    () => (selectionEnabled ? [selectionColumn<TData>(), ...columns] : columns),
    [selectionEnabled, columns],
  );

  const table = useAppTable<TData>({
    columns: tableColumns,
    data: page?.items ?? (EMPTY_ROWS as TData[]),
    rowCount: page?.totalCount,
    state: { pagination, sorting, rowSelection: rowSelection ?? {} },
    onPaginationChange: handlePaginationChange,
    onSortingChange: handleSortingChange,
    enableRowSelection: selectionEnabled,
    onRowSelectionChange: onRowSelectionChange ?? (() => {}),
    ...(getRowId ? { getRowId } : {}),
  });

  const columnCount = table.getAllLeafColumns().length;
  const rows = table.getRowModel().rows;

  return (
    <table.AppTable>
      <div className="flex flex-col gap-4">
        <DataTableToolbar
          state={state}
          onStateChange={onStateChange}
          filters={filters}
          searchable={searchable}
          searchPlaceholder={searchPlaceholder}
          actions={toolbarActions}
        />

        <div className="overflow-hidden rounded-lg border">
          {/* La tabla scrollea dentro de su contenedor: el cuerpo de la página nunca
              scrollea en horizontal. */}
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                {table.getHeaderGroups().map((group) => (
                  <TableRow key={group.id} className="hover:bg-transparent">
                    {group.headers.map((header) => (
                      <TableHead
                        key={header.id}
                        className={cn(
                          header.column.columnDef.meta?.align === "right" &&
                            "text-right",
                          header.column.columnDef.meta?.align === "center" &&
                            "text-center",
                        )}
                      >
                        {header.isPlaceholder ? null : (
                          <table.FlexRender header={header} />
                        )}
                      </TableHead>
                    ))}
                  </TableRow>
                ))}
              </TableHeader>

              <TableBody
                className={cn(
                  "transition-opacity",
                  // Recargando: se atenúa, no se vacía.
                  isFetching && !isPending && "opacity-60",
                )}
              >
                {error ? (
                  <DataTableError columns={columnCount} error={error} />
                ) : isPending ? (
                  <DataTableSkeletonRows
                    columns={columnCount}
                    rows={Math.min(state.pageSize, 8)}
                  />
                ) : rows.length === 0 ? (
                  <DataTableEmpty columns={columnCount} state={emptyState} />
                ) : (
                  rows.map((row) => (
                    <TableRow
                      key={row.id}
                      {...(onRowClick
                        ? {
                            onClick: () => onRowClick(row.original),
                            onKeyDown: (event: React.KeyboardEvent) => {
                              if (event.key === "Enter" || event.key === " ") {
                                event.preventDefault();
                                onRowClick(row.original);
                              }
                            },
                            tabIndex: 0,
                            role: "button",
                            className:
                              "focus-visible:ring-ring cursor-pointer focus-visible:ring-2 focus-visible:ring-inset focus-visible:outline-none",
                          }
                        : {})}
                    >
                      {row.getVisibleCells().map((cell) => (
                        <TableCell
                          key={cell.id}
                          className={cn(
                            cell.column.columnDef.meta?.align === "right" &&
                              "text-right tabular-nums",
                            cell.column.columnDef.meta?.align === "center" &&
                              "text-center",
                          )}
                        >
                          <table.FlexRender cell={cell} />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </div>

        <DataTablePagination
          page={page}
          state={state}
          onStateChange={onStateChange}
        />
      </div>
    </table.AppTable>
  );
}
