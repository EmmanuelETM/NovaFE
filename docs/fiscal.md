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

## Retenciones (ITBIS e ISR)

Aplican a los tipos **41** (Compras) y **47** (Pagos al Exterior): el emisor es
agente de retención y descuenta ITBIS y/o ISR del pago al proveedor. Van en el
área `<Retencion>` de cada línea y se suman en `<TotalITBISRetenido>` /
`<TotalISRRetencion>` del encabezado.

- **Tipo 41** — `<Retencion>` obligatoria por línea; `MontoITBISRetenido` y
  `MontoISRRetenido` son condicionales (se emiten si `> 0`).
- **Tipo 47** — `<Retencion>` obligatoria por línea, **solo ISR**: su XSD no tiene
  `MontoITBISRetenido`, y `MontoISRRetenido` es obligatorio (se emite aunque sea 0).
  El dominio rechaza un monto de ITBIS en la retención del 47.

**El monto retenido lo calcula y lo presenta el cliente, por línea.** El motor
fiscal solo **suma** lo que recibe (`EcfLineInput.ItbisWithheld` / `IsrWithheld`
→ `EcfTotals.TotalItbisWithheld` / `TotalIsrWithheld`). No deriva porcentajes.
Motivo: la tasa depende de datos que no están en el e-CF.

- **ITBIS retenido.** El ITBIS de la línea sí es conocido (sale del
  `IndicadorFacturacion` 18/16/0/exento, ya calculado en `EcfLineResult.TaxAmount`),
  pero el **porcentaje que se retiene** de ese ITBIS —típicamente **30 %** o
  **100 %**— depende de la clasificación del proveedor (persona física vs.
  sociedad, RST, Estado…), que el e-CF no lleva. Por eso el monto final lo pone
  el cliente. Un chequeo sano —hoy no implementado— sería
  `ItbisRetenido ≤ EcfLineResult.TaxAmount` de la misma línea.
- **ISR retenido.** No hay nada que inferir: la tasa varía por la naturaleza del
  pago —**2 %** (servicios en general / bienes), **5 %**, **10 %** (honorarios,
  alquileres), **27 %**— y el desglose de la renta lo tiene el sistema del
  cliente, no el comprobante. El **27 %** es exclusivo del tipo 47 (pago a
  no residentes). El motor toma el número tal cual.

Las retenciones **no** entran en `<MontoTotal>` (que es el valor de la factura):
son lo que se descuenta al pagar. El neto a pagar es
`MontoTotal − TotalITBISRetenido − TotalISRRetencion`, que el serializador emite
como `<ValorPagar>` en el tipo 41.

## ISC específico (integra la base del ITBIS)

El ISC **específico** (Tabla I: alcoholes 006-018, cigarrillos 019-022 — montos
fijos por volumen) lo **trae el cliente ya calculado**, por línea, en
`EcfAdditionalTax.IscEspecifico`. A diferencia del resto de impuestos adicionales,
la DGII exige aplicarlo **antes** del ITBIS (`contexto §5.2` nota 12, RF-06.4):

```
IndicadorMontoGravado=0:  ITBIS = Money((MontoItem + IscEspecifico) × tasa)
IndicadorMontoGravado=1:  base+ISC = Money(MontoItem / (1 + tasa));  ITBIS = MontoItem − (base+ISC)
                          MontoGravado = (base+ISC) − IscEspecifico
```

`<MontoGravadoI1/2/3>` **no** lleva el ISC (sigue siendo el `MontoItem`); el ISC
vive en `<MontoImpuestoAdicional>` vía `EcfTotals.TotalImpuestoSelectivoConsumo`.
Así `MontoTotal = MontoGravadoTotal + Exento + TotalITBIS + MontoImpuestoAdicional`
no lo cuenta dos veces — lo único que sube es `TotalITBIS`. Si un descuento global
de la Sección D toca ese bucket, el ITBIS se recalcula sobre `(gravado + isc) × tasa`.

El **ISC ad valorem** (`IscAdvalorem`) y `Otros` siguen "por encima", sin tocar la
base — su interacción con la base (RF-06.4 pasos 3-5) es un slice aparte.

## Alcance v1 y lo que falta

**Incluido:** ITBIS (18/16/0/exento), ajustes de línea, "otros impuestos
adicionales" que el cliente ya trae calculados (Propina Legal, CDT, ISC de
servicios, ISC ad valorem…), **ISC específico** que integra la base del ITBIS
(monto que trae el cliente), regla de los 30 días, chequeo de tolerancia,
`MontoNoFacturable`/`MontoPeriodo`, `FiscalRules.CreditNoteTotalWithinOriginal`,
la **totalización** de retenciones de ITBIS/ISR por línea (ver arriba), y la
**reconciliación mecánica de la Sección D**.

## Descuentos y recargos globales (Sección D)

`EcfCalculator.Calculate(lines, montoNoFacturable, globalAdjustments)` — el tercer
parámetro es una lista de `EcfGlobalAdjustmentInput(IsDiscount, AffectsRate, Amount,
Norma1007)`. **Después** de acumular los buckets de línea:

1. Por cada ajuste, el `Amount` se aplica al bucket que indica `AffectsRate`
   (1 → `MontoGravadoI1`, 2 → `I2`, 3 → `I3`, 4 → `MontoExento`): descuento resta,
   recargo suma.
2. Si el bucket es gravado (1 o 2), su ITBIS se **recalcula** sobre la nueva base
   (`gravadoIn × tasa`). El bucket 3 es 0 %. El exento no lleva ITBIS.
3. `MontoTotal` sale de los buckets ya ajustados → el `<Totales>` emitido cuadra.
4. Si un descuento deja un bucket en negativo → `Fiscal.GlobalAdjustmentExceedsBucket`.

**Norma 10-07** (`IndicadorNorma1007 = 1`, solo descuentos a la tasa 1 y solo en
31/32/33/34/45): el descuento **no** rebaja `MontoGravadoI1` ni `ITBIS1` ni
`MontoTotal` — se acumula en `EcfTotals.Norma1007Discount` y solo baja el
`<ValorPagar>` (`MontoTotal − retenciones − Norma1007Discount`). Formato notas 12,
27 y campo `<ValorPagar>` nota c.

**Slices aparte** (marcados en el código):

- **Derivación del ISC** de alcoholes y cigarrillos desde
  `GradosAlcohol`/`CantidadReferencia` — RF-06.4 (alcoholes) y RF-06.5
  (cigarrillos), con las tasas de la Tabla I ajustadas trimestralmente. Hoy el
  monto lo trae el cliente (ver "ISC específico" arriba); el ISC ad valorem y su
  interacción con la base (RF-06.4 pasos 3-5) también quedan acá.
- **Distribución de la Sección D a nivel de línea** (`MontoItem/ΣMontoItem`,
  Formato notas 28/29) — hoy la Sección D solo se reconcilia a nivel de bucket.
- **Cálculo** de las tasas de retención de ITBIS/ISR — a propósito fuera de
  alcance (ver "Retenciones" arriba): lo hace el cliente. Lo que sí podría
  agregarse es el chequeo `ItbisRetenido ≤ ITBIS de la línea`.
- **Otra moneda** — RF-06.9: hoy el cliente provee los `*OtraMoneda`; el motor
  solo hace cross-check (`docs/ecf-xml.md`). Derivarlos (`Money(dop / TipoCambio)`)
  está a debatir.
