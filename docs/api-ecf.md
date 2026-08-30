# API pública de emisión de e-CF — diseño del payload

**Estado: borrador de diseño. No implementado.** Este documento fija el contrato
antes de construir Módulo 2 (generación XML) y Módulo 12 (API pública).

Fuentes: `C:\workplace\FE_DGII\contexto-proyecto-fe-dgii.md` §5.3 (Formato e-CF
v1.0, oct 2025) y §5.4 (RFCE), y los XSD en `C:\workplace\FE_DGII\XSD\`.

---

## 1. Principios

**Modelo curado, no 1:1 con el XML.** El cliente manda *intención comercial*;
NovaFE produce el e-CF conforme. Vendemos "no tenés que volverte experto en
e-CF", así que el payload no expone los ~137 campos de encabezado ni la matriz de
obligatoriedad 0/1/2/3 por tipo.

### Lo que arma NovaFE (el cliente nunca lo manda)

| Campo XML | Módulo | Cómo |
|---|---|---|
| `<eNCF>` | 7 | Asignación atómica con lock (`INcfSequenceAllocator`) |
| `<FechaVencimientoSecuencia>` | 7 | 31-dic del año siguiente a la autorización; ausente en tipos 32 y 34 |
| `<RNCEmisor>`, `<RazonSocialEmisor>`, `<DireccionEmisor>`, sucursal, teléfonos… | — | Del tenant |
| Todos los `<Totales>` y `<MontoItem>` | 6 | `EcfCalculator` |
| `<TotalPaginas>`, sección Paginación | 9 | Del layout de la RI |
| `<IndicadorNotaCredito>` | 6 | Regla de 30 días desde `reference.modifiedNcfDate` |
| `<FechaHoraFirma>`, `<Signature>`, `CodigoSeguridad` | 3 | Al firmar |
| Campos `*OtraMoneda` | 6 | Desde `exchangeRate` |

### Lo que manda el cliente

Todo lo demás: comprador, líneas, pago, tipo de ingreso, ajustes globales,
referencia (NC/ND), exportación, transporte. Detalle en §4.

### Validación de totales (escape hatch de migración)

El cliente **puede** mandar sus totales calculados en `declaredTotals` y
`lines[].declaredAmount`. NovaFE los pasa por el chequeo de tolerancia de Módulo 6
(±1 por línea, tolerancia global = nº de líneas) y devuelve `toleranceWarning` si
no cuadran — **pero los valores de NovaFE son los que van al XML**. Un cliente que
viene de otro proveedor manda todo; uno nuevo manda el mínimo.

---

## 2. Endpoint

```
POST /api/v1.0/ecf
```

| | |
|---|---|
| **Auth** | `X-Tenant-Id` (hoy) / API key (Módulo 12) |
| **`Idempotency-Key`** (header) | Reintento seguro de la llamada HTTP. Devuelve la respuesta original idéntica. TTL ≥ 24 h. |
| **`internalNumber`** (body → `NumeroFacturaInterna`) | Dedup de negocio (RF-04.5). Si llega dos veces del mismo tenant en ≥ 24 h, devuelve el e-CF existente. |

Ambas claves cumplen roles distintos y se usan juntas.

Otros endpoints del recurso:

| | |
|---|---|
| `GET /api/v1.0/ecf/{id}` | Estado y detalle actual |
| `GET /api/v1.0/ecf/{id}/xml` | XML firmado (`application/xml`) |
| `GET /api/v1.0/ecf/{id}/ri` | Representación Impresa (PDF) |
| `GET /api/v1.0/ecf?...` | Listado / búsqueda |

---

## 3. Forma del request: un solo objeto, discriminado por `type`

El cliente ve **un solo objeto**. `type` (31…47) determina qué bloques son
obligatorios. En el servidor enrutamos por `type` a:

1. un validador FluentValidation por tipo (`Ecf31Validator`, `Ecf34Validator`…)
   que aplica la matriz de obligatoriedad de *ese* tipo;
2. un builder de dominio por tipo que arma el agregado `Ecf`.

El OpenAPI documenta cada tipo con `oneOf` + ejemplos; el body aceptado es el
mismo objeto.

---

## 4. Modelo del request

Campos marcados `opc.` son opcionales; el resto son obligatorios para los tipos
que los usan (ver §5).

### 4.1 Cabecera del documento

| Campo | Tipo | XML | Nota |
|---|---|---|---|
| `type` | int | `TipoeCF` | 31, 32, 33, 34, 41, 43, 44, 45, 46, 47 |
| `issueDate` | `dd-MM-yyyy` opc. | `FechaEmision` | Default: hoy (calendario RD). No puede ser futura ni anterior al alta como facturador |
| `incomeType` | string | `TipoIngresos` | `"01"`…`"06"`. Tipos 31, 33, 34, 41, 45, 46, 47 y el RFCE |
| `pricesIncludeTax` | bool opc. | `IndicadorMontoGravado` | Default del request; se puede sobrescribir por línea. `true` = el precio ya trae ITBIS |
| `deferredDelivery` | bool opc. | `IndicadorEnvioDiferido` | Solo contribuyentes móviles/offline autorizados |
| `currency` | string opc. | `TipoMoneda` | ISO (Tabla II). `null` = DOP |
| `exchangeRate` | decimal opc. | `TipoCambio` | 4 decimales. Obligatorio si `currency` ≠ null |
| `internalNumber` | string | `NumeroFacturaInterna` | Clave de dedup de negocio |
| `sellerCode` | string opc. | `CodigoVendedor` | |
| `nonInvoiceableAmount` | decimal opc. | `MontoNoFacturable` | Reembolsos, propina voluntaria. Puede ser negativo. Afecta `MontoPeriodo`, no `MontoTotal` |
| `additionalInfo.issuer` / `.buyer` | string opc. | `InformacionAdicional*` | Texto libre para la RI |

### 4.2 `buyer` (Comprador)

| Campo | XML | Nota |
|---|---|---|
| `rnc` | `RNCComprador` | RNC o cédula. Excluyente con `foreignId` |
| `foreignId` | `IdentificadorExtranjero` | Excluyente con `rnc` |
| `name` | `RazonSocialComprador` | |
| `email` opc. | `CorreoComprador` | |
| `phone` opc. | `TelefonoAdicional` | |
| `address` / `municipality` / `province` opc. | `DireccionComprador` / `MunicipioComprador` / `ProvinciaComprador` | Códigos Tabla III. Obligatorio en algunos tipos |

### 4.3 `payment`

| Campo | XML | Nota |
|---|---|---|
| `condition` | `TipoPago` | `"cash"` (1), `"credit"` (2), `"free"` (3) |
| `dueDate` opc. | `FechaLimitePago` | `dd-MM-yyyy`. Obligatorio si `condition = "credit"` |
| `methods[]` | `TablaFormasPago` | Hasta 7. `{ type, amount }`. `type`: `cash`, `check_transfer`, `card`, `credit`, `voucher`, `swap`, `credit_note`, `other` (1–8) |
| `account` opc. | `TipoCuentaPago` / `NumeroCuentaPago` / `BancoPago` | `{ type, number, bank }` |
| `billingPeriod` opc. | `FechaDesde` / `FechaHasta` | Servicios periódicos (utilities) |

### 4.4 `lines[]` (Detalle — hasta 1 000; 10 000 para 32 < DOP 250 k)

| Campo | XML | Nota |
|---|---|---|
| `name` | `NombreItem` | Obligatorio, se imprime en la RI |
| `kind` | `IndicadorBienoServicio` | `"good"` / `"service"` |
| `description` opc. | `DescripcionItem` | |
| `quantity` | `CantidadItem` | |
| `unitOfMeasure` | `UnidadMedida` | Código Tabla IV |
| `unitPrice` | `PrecioUnitarioItem` | Hasta 4 decimales |
| `itbisRate` | `IndicadorFacturacion` | 1 = 18 %, 2 = 16 %, 3 = 0 % gravado, 4 = exento |
| `priceIncludesTax` opc. | `IndicadorMontoGravado` | Sobrescribe el default del request |
| `codes[]` opc. | `TablaCodigosItem` | `{ type, value }` — GTIN, interno… |
| `discounts[]` opc. | `TablaSubDescuento` | Hasta 12. `{ valueType: "percent"\|"amount", value, description }` → NovaFE calcula `DescuentoMonto` |
| `surcharges[]` opc. | `TablaSubRecargo` | Hasta 12, misma forma |
| `additionalTaxes[]` opc. | `TablaImpuestosAdicionales` | Hasta 2. `{ code, rate?, amount? }` — Propina Legal (001), CDT (002), ISC… |
| `retention` opc. | Área Retención | `{ itbisAmount, isrAmount }` |
| `referenceQuantity` / `referenceUnit` / `referenceUnitPrice` opc. | `CantidadReferencia` / `UnidadReferencia` / `PrecioUnitarioReferencia` | Conversión de unidad; base del ISC de alcoholes |
| `alcoholDegrees` opc. | `GradosAlcohol` | ISC alcoholes |
| `manufactureDate` / `expiryDate` opc. | `FechaElaboracion` / `FechaVencimientoItem` | |
| `mining` opc. | Área Minería | Sector minero |
| `declaredAmount` opc. | `MontoItem` | El monto que calculó el cliente → tolerance check |

`numeroLinea` lo asigna NovaFE (1…N, sin saltos).

### 4.5 `globalAdjustments[]` (Sección D — hasta 20)

`{ kind: "discount"|"surcharge", valueType: "percent"|"amount", value, itbisRate, description, norm1007? }`.
Si el detalle mezcla tasas de ITBIS, `valueType` **debe** ser `"percent"`.
`norm1007` (Norma 10-07) solo aplica a tipo 31. NovaFE distribuye
proporcionalmente por `MontoItem / ΣMontoItem` (Módulo 6, slice posterior).

### 4.6 `reference` (Nota de Crédito / Débito, reemplazos)

| Campo | XML | Nota |
|---|---|---|
| `modifiedNcf` | `NCFModificado` | e-NCF (E+tipo+10 díg.) o NCF de papel. Debe haber sido enviado a la DGII antes |
| `modifiedNcfDate` | `FechaNCFModificado` | `dd-MM-yyyy` |
| `modificationCode` | `CodigoModificacion` | 1 = anula, 2 = corrige texto, 3 = corrige montos, 4 = reemplazo contingencia, 5 = referencia RFCE (solo tipo 31) |
| `reason` opc. | `RazonModificacion` | |
| `otherIssuerRnc` opc. | `RNCOtroContribuyente` | Solo si el RNC emisor no coincide con el del NCF modificado (baja, fusión) |

`<IndicadorNotaCredito>` (0 = dentro de 30 días, 1 = después) lo calcula NovaFE
con `modifiedNcfDate` y la fecha de emisión de la NC.

### 4.7 `export` (tipo 46) y `transport` (opcionales)

`export`: `fob`, `insurance`, `freight`, `otherCosts`, `cif`, `incoterms`,
puertos de embarque/salida/desembarque, `customsRegime`, pesos brutos/netos con
unidad, bultos, volumen → sección Informaciones Adicionales.

`transport`: `route`, `driver`, `plate`, `airWaybill`, `originCountry`,
`destinationCountry`, `destinationAddress`.

### 4.8 `declaredTotals` (opcional — tolerance check)

`{ montoGravadoTotal, montoGravadoI1, montoGravadoI2, montoGravadoI3, montoExento,
totalItbis, totalItbis1, totalItbis2, totalItbis3, montoImpuestoAdicional,
totalItbisRetenido, totalIsrRetenido, montoTotal, montoPeriodo }`. Todo opcional;
lo que venga se compara contra el cálculo de NovaFE.

### 4.9 `informativeSubtotals[]` (Sección C — opcional, poco frecuente)

Grupos informativos para la RI. No afectan la base imponible.

---

## 5. Campos obligatorios por tipo

| Tipo | Bloques obligatorios extra |
|---|---|
| **31** Crédito Fiscal | `buyer.rnc`, `incomeType` |
| **32** Consumo | `buyer` completo solo si `montoTotal ≥ 250 000`. < 250 k → se enruta a **RFCE** (NovaFE genera el resumen; el cliente igual manda un tipo 32 normal) |
| **33** Nota de Débito | `reference`, `incomeType`. Identificación del comprador: solo si el monto ≥ DOP 250 000 o si modifica un e-CF que identifica al comprador (31, 41, 44, 45…) |
| **34** Nota de Crédito | `reference` (con `modifiedNcf`, `modifiedNcfDate`, `modificationCode`). Identificación del comprador con la misma regla que el 33. Sin `FechaVencimientoSecuencia` |
| **41** Compras | `buyer.rnc` (proveedor informal), `incomeType` |
| **43** Gastos Menores | mínimo |
| **44** Regímenes Especiales | `buyer.rnc`, `incomeType` |
| **45** Gubernamental | `buyer.rnc` |
| **46** Exportaciones | `export`, `incomeType`, normalmente `currency` |
| **47** Pagos al Exterior | `buyer.foreignId`, `incomeType` |

---

## 6. Respuesta

```json
{
  "id": "0194f2c1-8a3e-7b21-9c44-1f2e3d4a5b6c",
  "status": "accepted",
  "encf": "E310000000042",
  "sequenceExpiresOn": "31-12-2027",
  "issueDate": "21-02-2026",
  "issuedAt": "2026-02-21T10:30:00-04:00",
  "signedAt": "2026-02-21T10:30:05-04:00",
  "securityCode": "aB3xK9",
  "qrUrl": "https://ecf.dgii.gov.do/ecf/consultatimbre?rncemisor=...&encf=E310000000042&...",
  "totals": {
    "montoGravadoTotal": 2000.00,
    "montoGravadoI1": 2000.00,
    "totalItbis": 360.00,
    "totalItbis1": 360.00,
    "montoExento": 0.00,
    "montoImpuestoAdicional": 0.00,
    "montoTotal": 2360.00,
    "montoNoFacturable": 0.00,
    "montoPeriodo": 2360.00
  },
  "toleranceWarning": null,
  "dgii": {
    "trackId": "d1e2f3a4-...",
    "status": "aceptado",
    "message": null,
    "respondedAt": "2026-02-21T10:30:07-04:00"
  },
  "commercialApproval": { "status": "pending" },
  "links": {
    "self": "/api/v1.0/ecf/0194f2c1-8a3e-7b21-9c44-1f2e3d4a5b6c",
    "xml": "/api/v1.0/ecf/0194f2c1-8a3e-7b21-9c44-1f2e3d4a5b6c/xml",
    "printedRepresentation": "/api/v1.0/ecf/0194f2c1-8a3e-7b21-9c44-1f2e3d4a5b6c/ri"
  }
}
```

- **Fechas**: los campos-documento (`issueDate`, `sequenceExpiresOn`) en
  `dd-MM-yyyy` (como la DGII); los timestamps del sistema (`issuedAt`,
  `signedAt`, `dgii.respondedAt`) en ISO 8601 con `-04:00`.
- **Sin XML inline** — está en `links.xml`.
- `dgii` es `null` mientras no haya respuesta de la DGII.
- `toleranceWarning` trae el detalle por línea si `declaredTotals` /
  `declaredAmount` no cuadraron; nunca bloquea la emisión.

---

## 7. Estados

### 7.1 `status` — ciclo de vida de NovaFE (contrato público)

```
                 ┌─────────────► rejected
signed ─► submitted ─► accepted
   │         │      └─► accepted_conditional
   │         └──────────► (sin respuesta) ─► processing ─► accepted | rejected | conditional
   └─► contingency ─► submitted (dentro de 72 h)
   └─► failed              (error de pipeline — requiere ops)
accepted ─► voided         (anulado luego por NC / ANECF)
```

| `status` | Significa |
|---|---|
| `signed` | Secuencia asignada, XML armado y firmado. Aún no enviado |
| `submitted` | Enviado a la DGII, esperando veredicto |
| `processing` | La DGII respondió "en proceso"; polling activo (RF-04.3: 30 s / 5 min / 30 min) |
| `accepted` | La DGII aceptó |
| `accepted_conditional` | Aceptado condicional (p. ej. cuadratura fuera de tolerancia) |
| `rejected` | La DGII rechazó. `dgii.message` trae el motivo. La secuencia se libera si `secuenciaUtilizada = false` |
| `contingency` | Firmado en contingencia; RI con leyenda; reenvío en 72 h |
| `failed` | El pipeline de NovaFE falló antes de enviar |
| `voided` | Anulado por una NC (tipo 34) o ANECF posterior |

### 7.2 `dgii` — respuesta cruda de la DGII

Se guarda tal cual para auditoría (Ley 32-23): `trackId`, código (`0` no
encontrado, `1` aceptado, `2` rechazado, + condicional), `message`,
`rawResponse`, `respondedAt`, contador de intentos de polling. **No** es el
contrato público; `status` lo es.

### 7.3 `commercialApproval` — ACECF (Módulo 5, posterior)

Tercera dimensión para tipos 31, 33, 34, 44, 45: `pending | approved | rejected`.
"Aprobado por el receptor" ≠ válido ante la DGII.

---

## 8. Flujo síncrono + asíncrono

`POST /api/v1.0/ecf`:

1. **Síncrono siempre** (todo local, < 1 s):
   asignar secuencia (M7) → armar XML (M2) → calcular totales (M6) → validar XSD
   → **firmar** (M3). Acá ya existen `encf`, `securityCode`, `qrUrl` y el XML
   firmado — todo lo que la RI necesita.
2. **Intento a la DGII con espera acotada** (~3–5 s):
   - Responde a tiempo → `status: accepted | rejected | accepted_conditional`,
     bloque `dgii` completo → **`201 Created`**.
   - Timeout / DGII lenta o en mantenimiento → al **outbox**,
     `status: received` (alias de `submitted` sin `dgii` aún) →
     **`202 Accepted`**. El worker termina el envío, hace polling y dispara
     **webhook** en cada transición.
3. Estado final: **webhook** (Módulo 12, HMAC-SHA256) o `GET /api/v1.0/ecf/{id}`.
   Un endpoint, no polling constante.

La emisión nunca "falla" desde el punto de vista del cliente por culpa de la
DGII: si firmamos, el comprobante existe y tiene RI. El veredicto de la DGII
llega después.

**Contingencia** (Módulo 11): si `statusecf.dgii.gov.do` indica ventana de
mantenimiento, se firma + se devuelve + `status: contingency` + RI con la leyenda
prescrita + reenvío automático dentro de 72 h.

---

## 9. Ejemplos

### 9.1 Tipo 31 — Factura de Crédito Fiscal

```json
{
  "type": 31,
  "incomeType": "01",
  "buyer": { "rnc": "131880681", "name": "Mi Cliente SRL", "email": "pagos@micliente.do" },
  "payment": {
    "condition": "credit",
    "dueDate": "15-03-2026",
    "methods": [{ "type": "check_transfer", "amount": 2360.00 }]
  },
  "lines": [
    {
      "name": "Servicio de consultoría",
      "kind": "service",
      "quantity": 1,
      "unitOfMeasure": "43",
      "unitPrice": 2000.00,
      "itbisRate": 1
    }
  ],
  "internalNumber": "FAC-2026-00042"
}
```

### 9.2 Tipo 32 < DOP 250 000 — Consumo (se enruta a RFCE)

```json
{
  "type": 32,
  "incomeType": "01",
  "pricesIncludeTax": true,
  "payment": { "condition": "cash", "methods": [{ "type": "card", "amount": 1180.00 }] },
  "lines": [
    { "name": "Almuerzo ejecutivo", "kind": "good", "quantity": 2,
      "unitOfMeasure": "43", "unitPrice": 500.00, "itbisRate": 1,
      "additionalTaxes": [{ "code": "001", "amount": 100.00 }] }
  ],
  "internalNumber": "POS-99812"
}
```

### 9.3 Tipo 34 — Nota de Crédito (corrige montos)

```json
{
  "type": 34,
  "incomeType": "01",
  "buyer": { "rnc": "131880681", "name": "Mi Cliente SRL" },
  "reference": {
    "modifiedNcf": "E310000000010",
    "modifiedNcfDate": "10-01-2026",
    "modificationCode": 3,
    "reason": "Error en el precio unitario facturado"
  },
  "lines": [
    { "name": "Ajuste de precio — Servicio de consultoría", "kind": "service",
      "quantity": 1, "unitOfMeasure": "43", "unitPrice": 200.00, "itbisRate": 1 }
  ],
  "internalNumber": "NC-2026-00007"
}
```

NovaFE calcula `<IndicadorNotaCredito>` (aquí: emisión hoy vs `10-01-2026` → si
≤ 30 días `0`, si no `1`), verifica `MontoTotal NC ≤ MontoTotal del E310000000010`
y omite `<FechaVencimientoSecuencia>`.

---

## 10. Pendiente / diferido

- ISC de alcoholes y cigarrillos que integra la base del ITBIS (M6, ver
  `docs/fiscal.md`).
- Distribución de descuentos/recargos globales Sección D + Norma 10-07 (M6).
- Retenciones — fórmulas de `ValorPagar` aún sin especificar en la doc DGII.
- Verificar contra el XSD: `IndicadorBienoServicio` (¿`1`/`2` o `B`/`S`?),
  códigos exactos de `TipoCuentaPago`, tabla de unidades de medida completa.
- Endpoints B2B como receptor (`/fe/...`, Módulo 5) — contrato aparte.
