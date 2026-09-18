"use client";

import { useState } from "react";
import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from "recharts";

import { DateRangePicker } from "@/components/shared/date-range-picker";
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

import { useFiscalSummary } from "./use-finance-summary";

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
 * Resumen fiscal del contribuyente — lado ventas, visibilidad interna (no un
 * formato de envío de la DGII, ver `docs/finance.md`). La UI nunca recalcula
 * dinero: todo lo que se ve acá ya viene sumado por la API.
 */
export function FinanceSummaryScreen() {
  const [range, setRange] = useState(() => ({
    from: addDays(today(), -30),
    to: today(),
  }));

  const {
    data: summary,
    isPending,
    error,
  } = useFiscalSummary(range.from, range.to);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex justify-end">
        <DateRangePicker from={range.from} to={range.to} onChange={setRange} />
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

          {summary.byType.length === 0 ? (
            <EmptyState
              title="Sin comprobantes en este rango."
              description="Elegí otro rango de fechas."
            />
          ) : (
            <Card>
              <CardHeader>
                <CardTitle>Facturado por tipo de e-CF</CardTitle>
                <CardDescription>
                  {RANGE_FORMAT.format(new Date(`${range.from}T12:00:00Z`))} –{" "}
                  {RANGE_FORMAT.format(new Date(`${range.to}T12:00:00Z`))}
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
    </div>
  );
}
