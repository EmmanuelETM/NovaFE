"use client";

import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from "recharts";

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
import { useFiscalSummary } from "./use-ecf";

const CHART_CONFIG = {
  totalAmount: { label: "Facturado", color: "var(--chart-1)" },
} satisfies ChartConfig;

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

          {summary.byType.length > 0 && (
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
                  <BarChart data={summary.byType}>
                    <CartesianGrid vertical={false} />
                    <XAxis
                      dataKey="typeName"
                      tickLine={false}
                      axisLine={false}
                      tickMargin={8}
                      fontSize={11}
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
                          formatter={(value) => formatMoney(Number(value))}
                        />
                      }
                    />
                    <Bar
                      dataKey="totalAmount"
                      fill="var(--color-totalAmount)"
                      radius={4}
                    />
                  </BarChart>
                </ChartContainer>

                <div className="mt-4 flex flex-wrap gap-2">
                  {summary.byType.map((row) => (
                    <Badge key={row.type} variant="outline" className="gap-1.5">
                      {row.typeName}
                      <span className="text-muted-foreground">
                        {formatCount(Number(row.count))}
                      </span>
                    </Badge>
                  ))}
                </div>
              </CardContent>
            </Card>
          )}
        </>
      )}

      <EcfTable state={state} onStateChange={setState} />
    </div>
  );
}
