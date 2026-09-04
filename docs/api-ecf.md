# API pública de emisión de e-CF (Módulo 12)

**Estado: implementado.** `POST /api/v1/ecf` arma, calcula, firma, persiste y
**envía el comprobante a la DGII** (Módulo 4): intenta resolver el estado fiscal
dentro del propio request (~8 s) y, si la DGII tarda, un worker de fondo termina
el seguimiento por `TrackId`. Ver `docs/dgii-submission.md`. Los webhooks (el
cliente hoy hace polling de `GET /ecf/{id}`) son un slice aparte.

Piezas: `IssueEcfCommand` (payload) · `EcfDocumentMapper` (payload → `EcfDocument`)
· `IssueEcfCommandValidator` · `IssueEcfUseCase` (pipeline) · `EcfController`.
Modelo persistido: `IssuedEcf` (tabla `issued_ecf`) — **no** confundir con
`EcfDocument`, el modelo fiscal transitorio.

---

## 1. Principios

**Modelo curado, no 1:1 con el XML.** El cliente manda *intención comercial*;
NovaFE produce el e-CF conforme. El payload no expone los ~137 campos de encabezado
ni la matriz de obligatoriedad 0/1/2/3 por tipo.

### Lo que arma NovaFE (el cliente nunca lo manda)

| Campo XML | De dónde |
|---|---|
| `<eNCF>` | Módulo 7 — asignación atómica con lock (`INcfSequenceAllocator`) |
| `<FechaVencimientoSecuencia>` | Del rango de secuencia asignado (`NcfAllocation.SequenceExpiresOn`); ausente en tipos 32 y 34 |
| `<RNCEmisor>`, `<RazonSocialEmisor>`, `<NombreComercial>` | Del `Tenant` |
| `<DireccionEmisor>`, `<Municipio>`, `<Provincia>`, `<TablaTelefonoEmisor>`, `<CorreoEmisor>`, actividad económica | Del **`EmitterProfile`** del tenant (ver §11) |
| Todos los `<Totales>` y `<MontoItem>` | Módulo 6 — `EcfCalculator` |
| `<IndicadorNotaCredito>` (0/1) | Módulo 6 — regla de 30 días desde `reference.modifiedNcfDate` |
| `<FechaHoraFirma>`, `<Signature>`, `CodigoSeguridad`, hash, QR | Módulo 3 — al firmar |
| Reconciliación de la **Sección D** en `<Totales>` | Módulo 6 — mecánica (ver `docs/fiscal.md`) |

### Lo que manda el cliente

Comprador, líneas, pago, tipo de ingreso, ajustes globales (Sección D), referencia
(NC/ND), `<OtraMoneda>` (montos **ya convertidos** — passthrough), embarque,
transporte, subtotales y paginación de la RI. Detalle en §4.

### Escape para migraciones (`declaredTotals` / `declaredAmount`)

El cliente **puede** mandar sus totales calculados en `declaredTotals` y
`lines[].declaredAmount`. NovaFE los pasa por el chequeo de tolerancia (±1 por
línea; encabezado: ±1 por campo) y devuelve `toleranceWarning` si no cuadran —
**los valores de NovaFE son los que van al XML**, nunca se rechaza (RF-06.6).

---

## 2. Endpoints

```
POST   /api/v1/ecf                 emitir
GET    /api/v1/ecf/{id}            estado y detalle
GET    /api/v1/ecf/{id}/xml        XML firmado (?rfce=true → el <RFCE>)
GET    /api/v1/ecf?...             listado / búsqueda paginado (filtro ?status=)
POST   /api/v1/ecf/{id}/retry      reencolar el envío (solo failed / review) → 202
```

| | |
|---|---|
| **Auth** | Header `X-API-Key` con un token `sk_nfe_…` del contribuyente (el tenant **y el ambiente** salen de la key). En Development también sirve `X-Tenant-Id`. Ver `docs/api-auth.md`. |
| **`Idempotency-Key`** (header, opcional) | Reintento seguro del `POST`. Misma clave + mismo cuerpo → **`200`** con la respuesta original. Misma clave + cuerpo distinto → **`409`**. Petición en curso → **`409`**. Tabla `idempotency_keys` en PostgreSQL. |
| **`internalNumber`** (body → `<NumeroFacturaInterna>`) | Dedup de negocio: un comprobante por `(tenant, internalNumber)` (índice único parcial). Repetido → **`200`** con el existente. |

---

## 3. Forma del request: un objeto, discriminado por `type`

El cliente ve **un solo objeto**. `type` (31…47) determina qué bloques son
obligatorios. En el servidor: `IssueEcfCommandValidator` (forma + reglas por tipo,
fail-fast) → `EcfDocumentMapper` → `EcfDocument.Create` (matriz de obligatoriedad
completa y invariantes fiscales — la autoridad).

Los campos "enum" (`payment.condition`, `payment.methods[].type`, `lines[].kind`,
`reference.modificationCode`, `transport.via`, `foreignCurrency.currency`, …)
aceptan el **nombre** (`"credit"`, `"check_transfer"`) o el **código DGII**
(`"2"`); `EcfPayloadEnum` los resuelve (ignora mayúsculas, `-` y `_`).

---

## 4. Modelo del request

### 4.1 Cabecera

| Campo | Tipo | XML | Nota |
|---|---|---|---|
| `type` | int | `TipoeCF` | 31, 32, 33, 34, 41, 43, 44, 45, 46, 47 |
| `issueDate` | `dd-MM-yyyy` opc. | `FechaEmision` | Default: hoy (calendario RD). No futura |
| `incomeType` | `"01"`…`"06"` | `TipoIngresos` | Tipos 31, 32, 33, 34, 44, 45, 46. **NO** 41/43/47 (su XSD no lo admite) |
| `pricesIncludeTax` | bool opc. | `IndicadorMontoGravado` | `true` = el precio ya trae ITBIS; se puede sobrescribir por línea |
| `deferredDelivery` | bool opc. | `IndicadorEnvioDiferido` | Solo autorizados |
| `nonInvoiceableAmount` | decimal opc. | `MontoNoFacturable` | Reembolsos, propina voluntaria. Puede ser negativo |
| `internalNumber` | string opc. | `NumeroFacturaInterna` | Clave de dedup de negocio |
| `sellerCode` | string opc. | `CodigoVendedor` | |
| `additionalInfo.issuer` / `.buyer` | string opc. | `InformacionAdicional*` | Texto libre para la RI |

### 4.2 `buyer` (opcional para el tipo 43)

`{ name, rnc, foreignId, email, contact, address, municipality, province, additionalInfo }`.
`rnc` y `foreignId` son **excluyentes**. Si `buyer` se omite, se usa "Consumidor Final".

### 4.3 `payment`

`{ condition, dueDate?, methods[] }`. `condition`: `cash` (1) / `credit` (2) /
`free` (3); `dueDate` (`dd-MM-yyyy`) obligatorio si `credit`. `methods[]`:
`{ type, amount }` — `type`: `cash`, `check_transfer`, `card`, `credit`, `voucher`,
`swap`, `credit_note`, `other` (1–8). Hasta 7.

### 4.4 `lines[]` (hasta 1000)

| Campo | XML | Nota |
|---|---|---|
| `name` | `NombreItem` | Obligatorio |
| `kind` | `IndicadorBienoServicio` | `"good"` (1) / `"service"` (2) |
| `quantity` | `CantidadItem` | ≥ 0 |
| `unitPrice` | `PrecioUnitarioItem` | ≥ 0, hasta 4 decimales |
| `itbisRate` | `IndicadorFacturacion` | 1 = 18 %, 2 = 16 %, 3 = 0 % gravado, 4 = exento |
| `unitOfMeasure` | `UnidadMedida` | Código Tabla IV |
| `description` opc. | `DescripcionItem` | |
| `discount` / `surcharge` opc. | `DescuentoMonto` / `RecargoMonto` | Monto final (las sub-tablas `TablaSubDescuento` son un slice posterior) |
| `priceIncludesTax` opc. | `IndicadorMontoGravado` | Sobrescribe el default del request |
| `codes[]` opc. | `TablaCodigosItem` | `{ type, value }` |
| `retention` opc. | Área `<Retencion>` | `{ agent, itbisWithheld, isrWithheld }` — `agent`: `withholding` (1) / `perception` (2). **Obligatoria** en cada línea de 41 y 47. Los montos los calcula el cliente (ver `docs/fiscal.md`) |
| `additionalTaxes[]` opc. | `TablaImpuestoAdicional` + `<ImpuestosAdicionales>` | `{ code, rate, iscEspecifico, iscAdvalorem, otros }` — código de la Tabla I (001–039). Solo 31/32/33/34/44/45. Todos los montos los trae el cliente. **`iscEspecifico`** (alcoholes 006-018, cigarrillos 019-022) **integra la base del ITBIS** de la línea: sube `<TotalITBIS>`, no `<MontoGravado>`; se totaliza en `<MontoImpuestoAdicional>` (todo en el XML firmado). `iscAdvalorem` y `otros` van por encima |
| `foreignCurrency` opc. | `<OtraMonedaDetalle>` | `{ unitPrice, discount, surcharge, lineAmount }` en divisa (passthrough) |
| `details` opc. | Campos opcionales del `<Item>` | `{ referenceQuantity, referenceUnit, subquantities[], alcoholDegrees, referenceUnitPrice, manufactureDate, expiryDate, mining }`. Passthrough — el ISC de alcoholes/cigarrillos igual va en `additionalTaxes` (la derivación desde `alcoholDegrees` es un slice posterior). `mining` solo en 32/33/34/46 |
| `declaredAmount` opc. | — | El `MontoItem` que calculó el cliente → chequeo de tolerancia |

`numeroLinea` lo asigna NovaFE (1…N).

### 4.5 `globalAdjustments[]` (Sección D — hasta 20)

`{ line, kind, affectsItbisRate, amount, norm1007?, description?, percentage? }`.
`kind`: `discount` (D) / `surcharge` (R). `affectsItbisRate`: 1/2/3/4 (a qué bucket
afecta). **El motor reconcilia mecánicamente** — aplica el monto al bucket y
recalcula su ITBIS; `MontoTotal` cuadra solo. `norm1007` (Norma 10-07) solo en
31/32/33/34/45, solo descuentos a la tasa 1 (no baja la base, solo `<ValorPagar>`).
No aplica a 43/47. La distribución proporcional **a nivel de línea** sigue pendiente.

### 4.6 `reference` (Notas de Crédito/Débito, reemplazos)

`{ modifiedNcf, modifiedNcfDate, modificationCode, otherIssuerRnc? }`.
`modificationCode`: `voids` (1), `corrects_text` (2), `corrects_amounts` (3),
`contingency_replacement` (4), `rfce_reference` (5 — solo tipo 31). Obligatoria en
33 y 34.

### 4.7 `foreignCurrency` (`<OtraMoneda>`) — passthrough

`{ currency, exchangeRate, totals: { montoGravadoTotal, montoGravadoI1..I3,
montoExento, totalItbis, totalItbis1..3, montoTotal } }`. El cliente trae los
montos **ya convertidos**; el motor solo hace un cross-check
`MontoTotal_DOP / exchangeRate` (tolerancia, nunca rechaza). Almacenamiento en DOP.
`currency`: código ISO (Tabla II). `exchangeRate > 0`.

### 4.8 `shipping` (`<InformacionesAdicionales>`) y `transport` (`<Transporte>`)

Passthrough. `shipping` no aplica a 41/43/47; `shipping.export` (FOB/CIF/puertos)
solo al tipo 46. `transport` no aplica a 41/43; el 47 solo admite
`destinationCountry`; el 46 agrega `via`/país/compañía transportista.

### 4.9 `subtotals[]` / `pagination[]`

Passthrough informativo para la RI (Sección C / `<Paginacion>`). No tocan ningún
total. `<Paginacion>` es condicional (solo facturas largas); el diseño original la
derivaba del layout de la RI, pero mientras Módulo 9 (renderizador de RI) no exista
no hay layout del que derivar — el cliente que pagina su propia RI la manda. Cuando
M9 la construya, este campo pasa a ser un override.

### 4.10 `declaredTotals` (opcional — tolerancia)

`{ montoGravadoTotal, montoExento, totalItbis, montoImpuestoAdicional, montoTotal }`.
Cada campo se compara con el cálculo de NovaFE (±1); si difiere → `toleranceWarning`
en la respuesta y la DGII probablemente devuelva "aceptado condicional".

---

## 5. Obligatoriedad por tipo (resumen)

| Tipo | Extra obligatorio |
|---|---|
| **31** Crédito Fiscal | `buyer.rnc`, `incomeType` |
| **32** Consumo | `buyer` solo si `montoTotal ≥ 250 000`; `< 250 000` → se emite como **RFCE** (NovaFE lo genera y lo firma; el cliente igual manda un tipo 32 normal) |
| **33** Nota de Débito | `reference`, `incomeType`; comprador si el monto ≥ 250 000 o si modifica un e-CF que lo identifica |
| **34** Nota de Crédito | `reference`; comprador con la misma regla que el 33; sin `FechaVencimientoSecuencia` |
| **41** Compras | `buyer.rnc`, `retention` en cada línea. Sin `incomeType` |
| **43** Gastos Menores | mínimo; sin `buyer`, líneas exentas, sin ajustes de línea |
| **44** Regímenes Especiales | `buyer.rnc`, `incomeType`, líneas exentas |
| **45** Gubernamental | `buyer.rnc`, `incomeType` |
| **46** Exportaciones | `incomeType`, líneas a tasa 0 %; normalmente `foreignCurrency` y `shipping.export` |
| **47** Pagos al Exterior | `buyer.foreignId`, `retention` (solo ISR) en cada línea. Sin `incomeType` |

---

## 6. Respuesta

```json
{
  "id": "0194f2c1-8a3e-7b21-9c44-1f2e3d4a5b6c",
  "status": "accepted",
  "encf": "E310000000042",
  "type": 31,
  "environment": "Test",
  "sequenceExpiresOn": "31-12-2027",
  "issueDate": "21-02-2026",
  "issuedAt": "2026-02-21T10:30:05-04:00",
  "signedAt": "2026-02-21T10:30:05-04:00",
  "securityCode": "aB3xK9",
  "qrUrl": "https://ecf.dgii.gov.do/testecf/consultatimbre?rncemisor=...",
  "submitsRfce": false,
  "internalNumber": "FAC-2026-00042",
  "toleranceWarning": null,
  "dgii": {
    "trackId": "TRACK-ABC-123",
    "status": "Aceptado",
    "statusCode": 1,
    "sequenceUsed": true,
    "messages": [],
    "submittedAt": "2026-02-21T10:30:06-04:00",
    "receivedAt": "2026-02-21T10:30:06-04:00",
    "processedAt": "2026-02-21T10:30:06-04:00"
  },
  "links": {
    "self": "/api/v1/ecf/0194f2c1-8a3e-7b21-9c44-1f2e3d4a5b6c",
    "xml":  "/api/v1/ecf/0194f2c1-8a3e-7b21-9c44-1f2e3d4a5b6c/xml",
    "rfceXml": null,
    "representation": "/api/v1/ecf/0194f2c1-8a3e-7b21-9c44-1f2e3d4a5b6c/representation"
  }
}
```

- **`201 Created`** en una emisión nueva (`Location` a `GET /ecf/{id}`); **`200 OK`**
  si la `Idempotency-Key` o el `internalNumber` ya se habían usado.
- El `status` de nivel superior es el estado de NovaFE; depende de qué alcanzó el
  envío dentro del presupuesto:

  | `status` | Cuándo |
  |---|---|
  | `accepted` / `accepted_conditional` / `rejected` | la DGII respondió dentro del presupuesto (`dgii.messages` trae las observaciones) |
  | `submitted` | enviado, hay `dgii.trackId`, la DGII sigue procesando — el worker termina |
  | `signed` | la DGII no respondió dentro del presupuesto — el worker enviará y hará polling |

- **La respuesta es identidad fiscal + estado + el intercambio con la DGII.** El
  detalle comercial (comprador, líneas, montos, retenciones) vive en el **XML
  firmado** — `links.xml` (`GET /ecf/{id}/xml`); `links.rfceXml` trae el `<RFCE>`
  cuando `submitsRfce` es `true`; `links.representation` es la **Representación
  Impresa en PDF** (`GET /ecf/{id}/representation`, ver `docs/representation.md`).
- **`dgii`** agrupa todo lo del envío y es `null` mientras no haya envío:
  `trackId`, `status` (el `estado` textual de la DGII, tal cual lo manda:
  "Aceptado", "Rechazado"…), `statusCode` (1 aceptado · 2 rechazado · 3 en proceso ·
  4 aceptado condicional), `sequenceUsed` (el `secuenciaUtilizada` de la DGII),
  `messages` (`{ code, value }` — el `value` en el idioma en que lo manda la DGII),
  y los instantes `submittedAt` (recepción confirmada), `receivedAt` (la
  `fechaRecepcion` que informó la DGII; ausente si no la dio, p. ej. el RFCE) y
  `processedAt` (resultado definitivo registrado por NovaFE).
- Fechas de documento (`issueDate`, `sequenceExpiresOn`) en `dd-MM-yyyy`; timestamps
  del sistema en ISO 8601 `-04:00`.

---

## 7. Estados

```
signed → submitted → accepted / accepted_conditional / rejected
   │          └──────► review   (la DGII no resolvió tras el ladder de polling)
   └───────► failed             (agotó el backoff de transporte, o rechazo del gateway)
```

`signed` = firmado y encolado. `review` y `failed` se reencolan con
`POST /ecf/{id}/retry` (→ `signed`, `202`). Detalle en `docs/dgii-submission.md`.
`contingency_pending` y `voided` (M8/M11) todavía no existen.

Si el pipeline falla **antes** de encolar (validación fiscal inesperada, XSD,
firma) el `POST` devuelve un error y el e-NCF se **quema** (se loguea, no se
persiste). Un `rejected` de la DGII también quema el número; el pool de secuencias
liberadas es un slice posterior (`docs/sequences.md`).

---

## 8. Flujo

`POST /api/v1/ecf`: resolver emisor + ambiente → idempotencia → dedup → asignar
secuencia (M7) → armar y calcular (M2 + M6) → firmar (M3) → **persistir + encolar
en una transacción** (M4). Luego el **fast-path inline** intenta el envío a la
DGII con espera acotada (~8 s); si no alcanza, un worker de fondo (outbox
`SKIP LOCKED`) lo termina. **Nunca** falla el `POST` por la DGII — el comprobante
ya está firmado y guardado. Los webhooks (HMAC-SHA256) son un slice aparte.

---

## 9. Ejemplos

### 9.1 Tipo 31 — Crédito Fiscal

```json
{
  "type": 31,
  "incomeType": "01",
  "internalNumber": "FAC-2026-00042",
  "buyer": { "rnc": "131880681", "name": "Mi Cliente SRL", "email": "pagos@micliente.do" },
  "payment": {
    "condition": "credit",
    "dueDate": "15-03-2026",
    "methods": [{ "type": "check_transfer", "amount": 2360.00 }]
  },
  "lines": [
    { "name": "Servicio de consultoría", "kind": "service", "quantity": 1,
      "unitOfMeasure": "43", "unitPrice": 2000.00, "itbisRate": 1 }
  ]
}
```

### 9.2 Tipo 32 < DOP 250 000 — Consumo (se emite como RFCE)

```json
{
  "type": 32,
  "incomeType": "01",
  "pricesIncludeTax": true,
  "payment": { "condition": "cash", "methods": [{ "type": "card", "amount": 1180.00 }] },
  "lines": [
    { "name": "Almuerzo ejecutivo", "kind": "good", "quantity": 2,
      "unitOfMeasure": "43", "unitPrice": 500.00, "itbisRate": 1 }
  ]
}
```

### 9.3 Tipo 34 — Nota de Crédito

```json
{
  "type": 34,
  "incomeType": "01",
  "buyer": { "rnc": "131880681", "name": "Mi Cliente SRL" },
  "reference": {
    "modifiedNcf": "E310000000010",
    "modifiedNcfDate": "10-01-2026",
    "modificationCode": "corrects_amounts",
    "otherIssuerRnc": null
  },
  "lines": [
    { "name": "Ajuste de precio", "kind": "service", "quantity": 1,
      "unitOfMeasure": "43", "unitPrice": 200.00, "itbisRate": 1 }
  ]
}
```

---

## 10. Fuera de alcance (Módulo 5 / 9 / 11 / 14 / webhooks)

- **Webhooks** — notificación al cliente del cambio de estado (config por tenant,
  HMAC-SHA256, reintentos, historial). Slice aparte; hoy el cliente hace polling.
- **M5** — envío al receptor electrónico B2B + ACECF / `commercialApproval`.
- **M11** — contingencia (`IndicadorEnvioDiferido`, Decreto 587-24).
- **RF-02.10** — validar NC ≤ `MontoTotal` del e-CF modificado (necesita el original persistido).
- **M9 (resto)** — `GET /ecf/{id}/ri` (PDF), bitmap del QR.
- **M14** — API keys.
- `payment.account` (`TipoCuentaPago`…) y `payment.billingPeriod` (`FechaDesde`/
  `FechaHasta`) — no existen en el dominio todavía.
- Sucursales del emisor (`<Sucursal>`); `TablaSubDescuento`/`TablaSubRecargo` de
  línea; derivación del ISC desde `details.alcoholDegrees`/`referenceQuantity`;
  validación de las tablas de códigos de la DGII (un código de provincia/municipio
  inválido en el `EmitterProfile` hoy falla el XSD post-firma con `500`).

---

## 11. Perfil fiscal del emisor (`EmitterProfile`)

El bloque `<Emisor>` del e-CF necesita dirección, municipio, provincia, teléfonos,
correo y actividad económica — datos que el `Tenant` no tiene. Se configuran una
vez por contribuyente (recurso de **operador**, no del cliente emisor):

```
GET  /api/v1/tenants/{id}/emitter-profile
PUT  /api/v1/tenants/{id}/emitter-profile   (upsert)
```

Cuerpo del `PUT`:

```json
{
  "address": "Av. 27 de Febrero 100, Santo Domingo",
  "municipality": "010100",
  "province": "010000",
  "phones": ["809-555-0100"],
  "email": "facturacion@almax.do",
  "economicActivity": "Comercio al por menor",
  "defaultEnvironment": "Test"
}
```

`municipality`/`province` son códigos de la Tabla III (6 dígitos).
`defaultEnvironment` es el ambiente DGII por defecto del contribuyente (`Test`
durante el onboarding, `Production` tras certificar): es el valor que hereda una
API key al acuñarse si no se le pasa uno, y el que se usa en el camino
`X-Tenant-Id` de Development. En una petición autenticada con API key el ambiente
lo fija la key, no el payload ni el perfil. Sin perfil configurado, `POST /ecf`
devuelve `400`.
