"use client";

import { Bar, BarChart, CartesianGrid, Cell, XAxis, YAxis } from "recharts";

import { DateRangePicker } from "@/components/shared/date-range-picker";
import { useTableSearchParams } from "@/components/shared/data-table";
import { EmptyState } from "@/components/shared/empty-state";
import { Badge } from "@/components/ui/badge";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart";
import { Skeleton } from "@/components/ui/skeleton";
import { addDays, today } from "@/lib/format/date";
import { formatCount, formatMoney } from "@/lib/format/money";

import { EcfTable } from "./ecf-table";
import { ecfTypeLabel, type FiscalSummaryByType } from "./types";
import { useFiscalSummary } from "./use-ecf";

/**
 * Color por naturaleza fiscal del tipo de e-CF, no por tipo individual —
 * son 10 tipos pero solo 3 categorías de flujo de dinero. `ingreso` reusa
 * `--chart-1` (ya era el color del gráfico); `notaCredito` reusa
 * `--destructive` (mismo rojo semántico que ya usa el resto de la app para
 * "atención/reversa"); `egreso` no tenía token propio, se agrega uno.
 */
const CHART_CONFIG = {
  totalAmount: { label: "Facturado" },
  ingreso: { label: "Ingresos", color: "var(--chart-1)" },
  notaCredito: { label: "Notas de crédito", color: "var(--destructive)" },
  egreso: {
    label: "Egresos",
    theme: { light: "#f59e0b", dark: "#fbbf24" },
  },
} satisfies ChartConfig;

type ChartColorKey = "ingreso" | "notaCredito" | "egreso";

/**
 * 34 (Nota de Crédito) es la única deducción/anulación. 41 (Compras a
 * informal), 43 (Gastos Menores) y 47 (Pagos al Exterior) son egresos del
 * propio contribuyente, no ventas. El resto (31/32/33 ventas normales,
 * 44 Regímenes Especiales, 45 Gubernamental, 46 Exportaciones) son ingresos.
 */
function chartColorKey(type: number): ChartColorKey {
  if (type === 34) return "notaCredito";
  if (type === 41 || type === 43 || type === 47) return "egreso";
  return "ingreso";
}

/** Le suma a cada fila la etiqueta corta (`31 · Crédito Fiscal`) para el eje X — la misma que ya usa la columna "Tipo" de la tabla. */
function chartData(byType: readonly FiscalSummaryByType[]) {
  return byType.map((row) => ({ ...row, label: ecfTypeLabel(row.type) }));
}

const RANGE_FORMAT = new Intl.DateTimeFormat("es-DO", {
  timeZone: "UTC",
  dateStyle: "medium",
});

function SummaryCard({
  title,
  value,
  description,
}: {
  title: string;
  value: string;
  description?: string;
}) {
  return (
    <Card>
      <CardHeader className="gap-1">
        <CardDescription>{title}</CardDescription>
        <CardTitle className="text-2xl font-semibold tabular-nums">
          {value}
        </CardTitle>
      </CardHeader>
      {description && (
        <CardContent className="text-muted-foreground text-xs">
          {description}
        </CardContent>
      )}
    </Card>
  );
}

/**
 * Comprobantes: resumen fiscal (cards + gráfico) del rango de fechas
 * elegido, y debajo el historial completo de emisiones — un solo selector
 * de rango filtra las dos cosas, porque son la misma pregunta ("¿qué pasó
 * en este período?") mirada en dos niveles de detalle. El resumen es
 * visibilidad interna, no un formato de envío de la DGII (ver
 * `docs/finance.md`); la UI nunca recalcula dinero, todo lo que se ve
 * acá ya viene sumado por la API.
 */
export function EcfScreen() {
  const [state, setState] = useTableSearchParams([
    "type",
    "status",
    "from",
    "to",
  ]);

  const from = state.filters.from ?? addDays(today(), -30);
  const to = state.filters.to ?? today();

  const { data: summary, isPending, error } = useFiscalSummary(from, to);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex justify-end">
        <DateRangePicker
          from={from}
          to={to}
          onChange={(range) =>
            setState({ filters: { from: range.from, to: range.to } })
          }
        />
      </div>

      {isPending ? (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {Array.from({ length: 4 }, (_, i) => (
            <Skeleton key={i} className="h-24 rounded-2xl" />
          ))}
        </div>
      ) : error || !summary ? (
        <EmptyState
          title="No se pudo cargar el resumen."
          description="Intentá de nuevo en un momento."
        />
      ) : (
        <>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <SummaryCard
              title="Total facturado"
              value={formatMoney(Number(summary.totalInvoiced))}
              description={`${formatCount(Number(summary.totalCount))} comprobantes`}
            />
            <SummaryCard
              title="ITBIS cobrado"
              value={formatMoney(Number(summary.totalItbis))}
            />
            <SummaryCard
              title="Retenciones (ITBIS + ISR)"
              value={formatMoney(
                Number(summary.totalItbisWithheld) +
                  Number(summary.totalIsrWithheld),
              )}
            />
            <SummaryCard
              title="Efecto neto NC/ND"
              value={formatMoney(Number(summary.netCreditDebitEffect))}
              description="Débito menos crédito"
            />
          </div>

          <Card>
            <CardHeader>
              <CardTitle>Facturado por tipo de e-CF</CardTitle>
              <CardDescription>
                {RANGE_FORMAT.format(new Date(`${from}T12:00:00Z`))} –{" "}
                {RANGE_FORMAT.format(new Date(`${to}T12:00:00Z`))}
              </CardDescription>
            </CardHeader>
            <CardContent>
              <ChartContainer config={CHART_CONFIG} className="h-64 w-full">
                <BarChart data={chartData(summary.byType)}>
                  <CartesianGrid vertical={false} />
                  <XAxis
                    dataKey="label"
                    tickLine={false}
                    axisLine={false}
                    tickMargin={8}
                    fontSize={11}
                    interval={0}
                  />
                  <YAxis
                    tickLine={false}
                    axisLine={false}
                    tickFormatter={(value: number) => formatMoney(value)}
                    width={90}
                  />
                  <ChartTooltip
                    content={
                      <ChartTooltipContent
                        labelKey="label"
                        formatter={(value, _name, _item, _index, row) => {
                          // El tipo de recharts para este 5º argumento es un
                          // union genérico de tooltip; en este gráfico
                          // siempre es la fila de datos del `<Cell>` activo.
                          const { count } =
                            row as unknown as FiscalSummaryByType;
                          return `${formatMoney(Number(value))} (${formatCount(Number(count))} comprobantes)`;
                        }}
                      />
                    }
                  />
                  <Bar dataKey="totalAmount" minPointSize={6} radius={4}>
                    {summary.byType.map((row) => (
                      <Cell
                        key={row.type}
                        fill={`var(--color-${chartColorKey(Number(row.type))})`}
                      />
                    ))}
                  </Bar>
                </BarChart>
              </ChartContainer>

              <div className="mt-4 flex flex-wrap gap-2">
                {summary.byType.map((row) => (
                  <Badge key={row.type} variant="outline" className="gap-1.5">
                    <span
                      aria-hidden
                      className="size-2 rounded-full"
                      style={{
                        backgroundColor: `var(--color-${chartColorKey(Number(row.type))})`,
                      }}
                    />
                    {ecfTypeLabel(row.type)}:{" "}
                    {formatMoney(Number(row.totalAmount))}{" "}
                    <span className="text-muted-foreground">
                      ({formatCount(Number(row.count))})
                    </span>
                  </Badge>
                ))}
              </div>
            </CardContent>
          </Card>
        </>
      )}

      <EcfTable state={state} onStateChange={setState} />
    </div>
  );
}
