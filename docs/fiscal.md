# Motor de cálculo fiscal (Módulo 6)

Dominio puro, sin E/S, sin reloj, determinístico. Vive en `src/Domain/Fiscal/`.
Calcula `<MontoItem>` por línea, todos los totalizadores del Encabezado del e-CF
y el análisis de tolerancia de cuadratura.

Fuentes verificadas: `C:\workplace\FE_DGII\contexto-proyecto-fe-dgii.md` §5.2–5.4
(contrastado contra los PDF oficiales) y `Plan Técnico Integral v2.0.txt` §7.
**Donde el Plan y el contexto difieren, manda el contexto** — el Plan es un
borrador pre-validación con errores documentados (contexto §9).

## Redondeo (`EcfRounding`)

Regla DGII (Informe Técnico e-CF, RF-06.1): se mira el dígito siguiente a la
posición conservada; **≥ 5 sube, < 5 trunca**. Es exactamente
`Math.Round(x, n, MidpointRounding.AwayFromZero)`. Todo en `decimal`.

| Campo | Decimales | Helper |
|---|---|---|
| Dinero, ITBIS, impuestos, descuentos, recargos | 2 | `Money` |
| `PrecioUnitarioItem`, `PrecioUnitarioItemOtraMoneda`, `TipoCambio` | 4 | `UnitPrice` |
| `Subcantidad` | 3 | `Subquantity` |

## ITBIS (`ItbisRate`, `EcfCalculator`)

`<IndicadorFacturacion>`: `1`=18 %, `2`=16 %, `3`=0 % gravado (con crédito
fiscal), `4`=Exento (sin crédito). La tasa 3 y la 4 no llevan ITBIS pero se
totalizan distinto: **3 va en `MontoGravadoI3`, 4 va en `MontoExento`**.

Por línea:

```
MontoItem = Money(PrecioUnitario × Cantidad − DescuentoMonto + RecargoMonto)

Exento (tasa 4):          MontoExento += MontoItem
IndicadorMontoGravado=1:  base = Money(MontoItem / (1 + tasa));  ITBIS = MontoItem − base
IndicadorMontoGravado=0:  base = MontoItem;                      ITBIS = Money(MontoItem × tasa)
```

Con `IndicadorMontoGravado=1` el ITBIS se saca como el resto (`MontoItem − base`)
para que la línea cuadre exacto al centavo.

## Totalizadores (`EcfTotals`)

Cuadratura **verificada contra el Formato e-CF v1.0 (oct 2025)**, no contra el
Plan (que metía `MontoNoFacturable` dentro de `MontoTotal` por error):

```
MontoGravadoTotal      = MontoGravadoI1 + MontoGravadoI2 + MontoGravadoI3
TotalItbis             = Itbis1 + Itbis2 + Itbis3          (Itbis3 siempre 0)
MontoImpuestoAdicional = TotalImpuestoSelectivoConsumo + TotalOtrosImpuestosAdicionales
MontoTotal             = MontoGravadoTotal + MontoExento + TotalItbis + MontoImpuestoAdicional
MontoPeriodo           = MontoTotal + MontoNoFacturable   ← MontoNoFacturable puede ser negativo
```

## `<IndicadorNotaCredito>` — regla de los 30 días (`CreditNoteIndicator`)

Solo tipo 34. **Valores `0` y `1`** (el Plan decía 1/2 — es un error, confirmado
contra el contexto):

- `0` — la NC se emite **dentro** de los 30 días de calendario de la fecha de
  emisión del comprobante modificado; el comprador conserva el derecho a la
  devolución del ITBIS. **30 días exactos todavía cuenta como "dentro".**
- `1` — se emite **después** de los 30 días.

Las fechas se cuentan en calendario dominicano (`GetDominicanToday()` en el
llamador). Una NC con fecha anterior al original es un error.

## Tolerancia de cuadratura (`EcfToleranceReport`, RF-06.6 corregido)

- Por línea: `|MontoItem calculado − MontoItem suministrado|` ≤ **1**.
- Global: la tolerancia es la **cantidad de líneas** del e-CF (no ±1 peso global).
- **Nunca es motivo de rechazo local.** Si se excede, la DGII marca el e-CF como
  *aceptado condicional*; el motor solo lo anticipa (`ExpectConditionalAcceptance`)
  para avisar al cliente. El e-CF se envía igual.

## Alcance v1 y lo que falta

**Incluido:** ITBIS (18/16/0/exento), ajustes de línea, "otros impuestos
adicionales" que el cliente ya trae calculados (Propina Legal, CDT…), regla de
los 30 días, chequeo de tolerancia, `MontoNoFacturable`/`MontoPeriodo`,
`FiscalRules.CreditNoteTotalWithinOriginal`, y la **totalización** de retenciones
de ITBIS/ISR por línea (`EcfLineInput.ItbisWithheld`/`IsrWithheld` →
`<TotalITBISRetenido>`/`<TotalISRRetencion>`; montos que trae el cliente).
Las retenciones **no** entran en `MontoTotal` — son lo que el emisor retiene al
pagar (tipo 41); el neto es `MontoTotal − retenciones` (`<ValorPagar>`).

**Slices aparte** (marcados en el código):

- **ISC de alcoholes y cigarrillos** (Tabla I códigos 006-039). El ISC
  *específico* **integra la base imponible del ITBIS** (`base → +ISC → ×tasa`);
  las fórmulas de derivación desde `GradosAlcohol`/`CantidadReferencia` son
  RF-06.4 (alcoholes) y RF-06.5 (cigarrillos). Hoy `TotalImpuestoSelectivoConsumo`
  siempre es 0.
- **Descuentos y recargos globales (Sección D)** — RF-06.8. Se distribuyen
  proporcionalmente por `MontoItem / ΣMontoItem` y afectan la base imponible;
  la excepción Norma 10-07 (solo tipo 31) no rebaja el gravado a tasa 1.
- **Cálculo** de las tasas de retención de ITBIS/ISR (30 %/100 % de ITBIS,
  10 %/2 % de ISR según el servicio, Norma 07-2007 y otras) — las fórmulas no
  están en la documentación procesada. Hoy el motor solo **suma** los montos
  retenidos que trae el cliente por línea.
- **Otra moneda** — RF-06.9: calcular en DOP y dividir por `TipoCambio`.
