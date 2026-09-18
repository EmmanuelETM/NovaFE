# Resumen fiscal (módulo de finanzas)

`GET /api/v1/finance/summary?from=&to=` — visibilidad interna del lado
**ventas** (lo que el contribuyente emitió), no un formato de envío de la
DGII. Recurso de tenant, política `EcfRead` (los mismos 3 roles que ya leen
comprobantes).

## Alcance

- **Solo ventas.** El lado compras (todo lo que el contribuyente recibió de
  terceros) depende de que exista el receptor B2B (M5, sin empezar) — fuera
  de alcance hasta entonces.
- **No es 606/607/608.** Es un panel de visibilidad para el contribuyente,
  no el formato exacto de envío de datos de la DGII — ese mapeo de campos
  nunca se consiguió (ni en `docs/fiscal.md` ni en la carpeta de referencia
  del proyecto) y es una dependencia externa, no de código. Si en algún
  momento se necesita generarlo, hay que conseguir el spec real primero.
- Un e-CF `rejected` no cuenta en ningún total: nunca quedó facturado ante
  la DGII.

## Cómo se calcula

`IssuedEcf.Totals` (Módulo 6) se persiste como un único `jsonb` — no hay
columnas SQL individuales por impuesto. `FinanceReadRepository`
(`src/Infrastructure/Finance/Sql/`) agrega con extracción `jsonb`
(`totals->>'TotalItbis1'` etc.) más `SUM`/`COUNT`, acotado por
`tenant_id` + `issue_date BETWEEN` (ya indexados). Deliberadamente **no**
se migraron columnas nuevas para los totales: el proyecto no tiene
clientes reales todavía, así que no hay datos de volumen real que
justifiquen esa optimización — es un cambio reversible si hace falta más
adelante.

`GetFiscalSummaryUseCase` (`src/Application/Finance/GetFiscalSummary/`)
llama al repositorio dos veces (totales generales +desglose por tipo de
e-CF) y arma `FiscalSummaryDto`, incluido `NetCreditDebitEffect` (nota de
débito menos nota de crédito, ya calculado del lado servidor — la UI nunca
recalcula dinero).

## La correlación NC/ND → original

`IssuedEcf.ModifiedNcf` (columna nueva, migración `AddModifiedNcfToIssuedEcf`)
guarda el `<NCFModificado>` al emitir — antes solo vivía transitorio en
`EcfDocument.Reference`, sin persistirse en ningún lado (se usó una vez, al
emitir, para el fix de RF-02.10: validar que la NC no supere el monto del
original). Sin esta columna, correlacionar notas de crédito/débito con lo
que modifican requeriría parsear `EcfXml` por fila — frágil y caro. No hay
backfill de e-CF ya emitidos antes de este cambio: el proyecto no tiene
clientes reales todavía, no hay volumen histórico que perder.

## Frontend

`/finanzas` (self-service, `web/app/tenant/[tenantId]/finanzas/`) —
selector de rango de fechas + tarjetas de totales + un gráfico (primer uso
real de `components/ui/chart.tsx`, ya generado pero sin consumidores hasta
ahora). Ver `web/features/finance/`.
