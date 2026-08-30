# Generación y validación del XML del e-CF (Módulo 2)

Convierte los datos fiscales en el `<ECF>` XML conforme a los XSD oficiales de la
DGII. Fuente de verdad: los XSD en `C:\workplace\FE_DGII\XSD\` — **el Plan Técnico
tiene errores, el XSD no**.

## Piezas

| Interfaz (Application) | Impl (Infrastructure) | Rol |
|---|---|---|
| — | `EcfDocument` (Domain, `src/Domain/Ecf`) | Modelo fiscal validado. `Create(...)` valida estructura y calcula todos los totales con Módulo 6 (`EcfCalculator`). Un documento construido siempre está cuadrado. |
| `IEcfXmlSerializer` | `EcfXmlSerializer` | Serializa a `<ECF>` con el orden del XSD, sin tags vacíos, escape DGII, formato numérico/fechas. Incluye `<FechaHoraFirma>`, **no** `<Signature>`. |
| `IEcfXsdValidator` | `EcfXsdValidator` | Valida contra el XSD embebido del tipo. |

`EcfDocument` **no es** el payload de la API (`docs/api-ecf.md`, curado). Es el
modelo interno que mapea 1:1 al XML: enums en vez de strings mágicos, `decimal` en
vez de strings formateados, `DateOnly` en vez de `"dd-MM-yyyy"`.

## Reglas del serializador

- **Orden de elementos**: exacto del XSD del tipo. Los opcionales sin valor se
  omiten (RF-02.5) — nunca `<Campo/>` ni `<Campo></Campo>`.
- **Escape (RF-02.3, 8 caracteres)**: `< > &` los hace `XmlWriter`; `" ' © ® €`
  los completa un post-proceso sobre el cuerpo (el e-CF no tiene atributos, así
  que es seguro). `© ® €` van como referencias numéricas (`&#169;` etc.).
  - ⚠️ El round-trip por `XmlDocument` de Módulo 3 (firma) normaliza `" ' © ® €` de
    vuelta a su forma literal en el XML firmado. Por C14N eso es idénticamente
    equivalente y la DGII valida la forma canonicalizada. Si TesteCF muestra que
    rechaza los literales, se agrega un pase de escape final en Módulo 3.
- **Números** (`EcfXmlFormat`): punto decimal, sin separador de miles, sin
  notación científica, sin ceros de más. 2 decimales para dinero, 4 para
  `PrecioUnitarioItem`/`TipoCambio`, 3 para `Subcantidad`. `<ITBIS1/2/3>` es la
  tasa como **entero** (18, 16, 0).
- **Fechas**: `dd-MM-yyyy` (campos de documento), `dd-MM-yyyy HH:mm:ss` GMT-4
  (`FechaHoraFirma`, vía `DominicanTimeZone`).
- Raíz `<ECF>` **sin namespace** (el XSD no tiene `targetNamespace`).
- Salida: `Indent=false`, sin BOM, con declaración `<?xml … UTF-8?>` — igual que
  lo que reserializa Módulo 3.

## Validación XSD

Los XSD oficiales van **vendorizados y embebidos** en
`src/Infrastructure/Ecf/Xsd/*.xsd` (`<EmbeddedResource>`). `EcfXsdValidator`
compila un `XmlSchemaSet` por tipo (cacheado) y recorre el XML con
`ValidationType.Schema`.

El e-CF **pre-firma no valida solo**: el XSD exige `<xs:any minOccurs="1">`
después de `<FechaHoraFirma>` (el hueco de la firma). La validación real corre
después de firmar (Módulo 3/4). Las pruebas le agregan una `<Signature>` de
relleno.

## Alcance v1

**Incluido:** **los diez tipos** (31–34, 41, 43–47) con IdDoc, Emisor, Comprador,
Totales, DetallesItems, InformacionReferencia, Retencion, **InformacionesAdicionales**,
**Transporte**, **OtraMoneda** y **DescuentosORecargos** (Sección D). Descuentos/
recargos de línea (`DescuentoMonto`/`RecargoMonto` directos), múltiples tasas de
ITBIS, tipos de item (bien/servicio), formas de pago.

**Bloques transversales** (decisión: passthrough — el cliente trae los datos, el
motor fiscal no los usa; ver plan):

- **`InformacionesAdicionales`** (`EcfHeader.Shipping` → `EcfShippingInfo`) — datos
  de embarque. Opcional en 31/32/33/34/44/45/46; el dominio lo rechaza en 41/43/47
  (`Ecf.BlockNotApplicable`). Los campos de exportación (FOB/CIF, puertos,
  `EcfExportDetails`) van en un sub-record y **solo aplican al tipo 46**
  (`Ecf.ExportFieldsOnlyForExports`). Se emite entre `</Comprador>` y `<Transporte>`.
- **`Transporte`** (`EcfHeader.Transport` → `EcfTransport`) — opcional en todos
  menos 41/43. El tipo 46 antepone vía/país/compañía transportista; el tipo 47 solo
  admite `<PaisDestino>` (`Ecf.TransportForPagosExteriorIsDestinationOnly`).
  `TransportVia` es smart enum (01 terrestre / 02 marítimo / 03 aérea, solo 46).
- **`OtraMoneda`** (`EcfHeader.ForeignCurrency` → `EcfForeignCurrency`) — facturación
  en divisa; condicional (obligatoriedad 2, todos los tipos). **Passthrough:** el
  cliente trae `TipoCambio` + los `*OtraMoneda` ya convertidos (subconjunto según el
  `<Totales>` del tipo: full ITBIS en 31/32/33/34/41/45; solo exento+total en
  43/44/47; solo bucket 0 % en 46). El almacenamiento sigue en DOP. `EcfDocument`
  hace un **cross-check** `MontoTotal_DOP / TipoCambio` vs. el declarado
  (`ForeignCurrencyCheck`, tolerancia 1 divisa/línea) — **nunca rechaza**. Solo se
  rechaza tipo de cambio ≤ 0 (`Ecf.InvalidExchangeRate`) o una línea en divisa sin
  el bloque de encabezado (`Ecf.LineForeignCurrencyWithoutHeader`). `OtraMonedaDetalle`
  (nivel línea, `EcfLine.ForeignCurrency`) es igual en los diez tipos. `CurrencyCode`
  smart enum = Tabla II (USD, EUR, …). *(Los campos `Currency`/`ExchangeRate` sueltos
  de `EcfHeader` se reemplazaron por este record.)*
- **`<OtraMoneda>` — derivación pendiente de debatir:** hoy el cliente provee los
  montos en divisa; una alternativa es derivarlos (`Money(dop / TipoCambio)`) para
  garantizar consistencia con `<Totales>`.
- **`DescuentosORecargos`** (Sección D, `EcfHeader.GlobalAdjustments` →
  `EcfGlobalAdjustment`) — descuentos/recargos globales; sección **condicional**
  (obligatoriedad 2) en todos menos 43/47 (`Ecf.BlockNotApplicable`). Va tras
  `</DetallesItems>`. **El motor los reconcilia mecánicamente** (`docs/fiscal.md`
  § Sección D): el monto se aplica al bucket que indica `AffectsRate` (un
  `ItbisRate`) y se recalcula su ITBIS; `MontoTotal` cuadra solo. `AdjustmentKind`
  smart enum D/R. `IndicadorNorma1007` solo en 31/32/33/34/45 y solo para
  descuentos a la tasa 1 (`Ecf.Norma1007NotApplicable`) — no baja la base, solo el
  `<ValorPagar>`. Hasta 20, `NumeroLinea` secuencial
  (`Ecf.NonContiguousGlobalAdjustmentLines`).

**Tipo 32 (Factura de Consumo)** — `<IdDoc>` como el 31 pero **sin
`<FechaVencimientoSecuencia>`** (`EcfType.HasSequenceExpiry` = false, igual que el
34); mantiene `<TablaFormasPago>`. `TipoIngresos` es obligatorio (XSD
`minOccurs="1"`; el dominio lo exige). El comprador solo se identifica si
`MontoTotal ≥ DOP 250 000` (ver `EcfDocument.RequiresBuyerIdentification`). No hace
la bifurcación al formato reducido RFCE — sigue pendiente.

**Tipo 33 (Nota de Débito)** — `<IdDoc>` idéntico al 31 (mantiene
`<FechaVencimientoSecuencia>` y `<TablaFormasPago>`); lo propio es que
`<InformacionReferencia>` es obligatoria (el dominio ya lo exige para 33 y 34).
No lleva `<IndicadorNotaCredito>`.

**Tipo 34 (Nota de Crédito)** — el `<IdDoc>` cambia respecto al 31: lleva
`<IndicadorNotaCredito>` (0 = dentro de 30 días calendario del e-CF modificado,
1 = después; lo calcula `CreditNoteIndicator` en el dominio) en lugar de
`<FechaVencimientoSecuencia>`, y su XSD **no admite `<TablaFormasPago>`** en el
IdDoc. `<InformacionReferencia>` es obligatoria. El resto de bloques es idéntico al 31.

**Tipo 41 (Compras)** — el emisor registra una compra a un proveedor informal y
actúa como agente de retención. El `<IdDoc>` **no lleva `<TipoIngresos>`** (ni
`<IndicadorEnvioDiferido>`); mantiene `<FechaVencimientoSecuencia>` y
`<TablaFormasPago>`. Cada `<Item>` lleva el área **`<Retencion>`** obligatoria
(`EcfLineRetention` en el dominio: agente 1/2 + `MontoITBISRetenido` /
`MontoISRRetenido`). **Los montos de retención los calcula y los presenta el
cliente** — el motor solo los suma; ver `docs/fiscal.md` § Retenciones para el
porqué (tasa de ISR variable, % de ITBIS según el proveedor). `<Totales>` agrega
`<TotalITBISRetenido>`, `<TotalISRRetencion>` y `<ValorPagar>` (= `MontoTotal −
retenciones`). Las retenciones **no** tocan `MontoTotal`. El tipo 47 reusa este
modelo (solo ISR); los demás tipos v1 rechazan `<Retencion>` en las líneas
(`Ecf.RetentionNotApplicable`).

**Tipo 43 (Gastos Menores)** — caja chica: el comprobante **más reducido**. Su
`<IdDoc>` lleva solo `TipoeCF`, `eNCF`, `FechaVencimientoSecuencia` y `TipoPago`
(nada de `IndicadorMontoGravado`, `TipoIngresos`, `FechaLimitePago`,
`TablaFormasPago`…). **No tiene bloque `<Comprador>`** (es un gasto propio del
emisor). `<Totales>` es solo `<MontoExento>` + `<MontoTotal>` (+ `MontoPeriodo`
opcional). Las líneas **no admiten** descuento, recargo ni otros impuestos, y el
encabezado **no admite** monto no facturable. El dominio rechaza todo eso
(`Ecf.OnlyExemptLinesAllowed`, `Ecf.LineAdjustmentsNotApplicable`,
`Ecf.NonInvoiceableAmountNotApplicable`). `EcfHeader.Buyer` sigue siendo obligatorio
en el record pero no se serializa para el 43.

**Tipo 44 (Regímenes Especiales)** — zona franca / regímenes de incentivo: **todo
es exento**. Su `<Totales>` **no tiene campos gravados ni de ITBIS** (solo
`<MontoExento>`, `<MontoImpuestoAdicional>`, `<MontoTotal>`…) y su `<IdDoc>` no
lleva `<IndicadorMontoGravado>`. El dominio rechaza cualquier línea no exenta
(`Ecf.OnlyExemptLinesAllowed`); `<TipoIngresos>` es obligatorio (XSD `minOccurs=1`).
El serializador omite los totales gravados por sí solo (guardas `> 0`), solo hace
falta la rama que salta `<IndicadorMontoGravado>`.

**Tipo 45 (Gubernamental)** — venta a una entidad del Estado. El XSD es
**idéntico al del 31** (IdDoc, Totales completos con ITBIS, `<InformacionReferencia>`
opcional). `RNCComprador` es `minOccurs="1"` (siempre); el 45 **no** tiene
`<IdentificadorExtranjero>`. Sin rama propia en el serializador.

**Tipo 46 (Exportaciones)** — venta al exterior, **siempre a ITBIS tasa 0 %**
(Formato nota 51: `IndicadorFacturacion = 3`; el dominio rechaza cualquier otra
tasa con `Ecf.OnlyZeroRatedLinesAllowed`). Su `<Totales>` solo tiene el bucket I3
(`MontoGravadoI3`, `ITBIS3`, `TotalITBIS3`) — sin I1/I2, sin `MontoExento`, sin
`MontoImpuestoAdicional` — y su `<IdDoc>` no lleva `<IndicadorMontoGravado>` (misma
rama que el 44). El comprador puede identificarse por `RNCComprador` **o**
`IdentificadorExtranjero` (o ninguno en casos de Zona Franca Comercial); solo
`RazonSocialComprador` es obligatorio. **Moneda:** el XML puede ir enteramente en
DOP — `<OtraMoneda>` es *condicional* a facturar en divisa (código 2 en la matriz
del Formato, igual que todos los tipos), no un requisito del 46. `<OtraMoneda>`,
`<PaisComprador>` y el bloque `<InformacionesAdicionales>` de exportación (FOB,
seguro, flete, CIF, puertos) van en slices posteriores.

**Tipo 47 (Pagos al Exterior)** — pago a un no residente; el emisor retiene ISR.
Líneas **exentas** (Formato nota 50). Su `<IdDoc>` no lleva `<TipoIngresos>`,
`<IndicadorMontoGravado>` ni `<IndicadorEnvioDiferido>`. `<Comprador>` es un bloque
**reducido**: solo `<IdentificadorExtranjero>` y `<RazonSocialComprador>`. Cada
`<Item>` lleva `<Retencion>` **obligatoria de solo ISR** —
`<IndicadorAgenteRetencionoPercepcion>` + `<MontoISRRetenido>` (siempre presente,
aunque sea 0); su XSD **no tiene `<MontoITBISRetenido>`** (el dominio rechaza ITBIS
en la retención del 47 con `Ecf.ItbisRetentionNotApplicable`). `<Totales>` = solo
`<MontoExento>`, `<MontoTotal>`, `<ValorPagar>` (= `MontoTotal − ISR`) y
`<TotalISRRetencion>`. Comparte con el 43 el rechazo de ajustes de línea y de monto
no facturable (`Ecf.LineAdjustmentsNotApplicable`, `Ecf.NonInvoiceableAmountNotApplicable`).

**Verificado contra el XSD**: `IndicadorBienoServicio` es **1 = Bien, 2 = Servicio**
(el contexto viejo decía B/S). `RNCValidationType` son 9 u 11 dígitos (no 10).
`IndicadorFacturacion` admite `0` ("No Facturable"). En los tipos 32/33/34
`RNCComprador` y `RazonSocialComprador` son `minOccurs="0"` (no estructural). El
tipo 44 no tiene campos gravados en `<Totales>` ni `<IndicadorMontoGravado>`; el
tipo 45 es estructuralmente el 31 pero sin `<IdentificadorExtranjero>` en el
comprador; el tipo 46 solo tiene el bucket I3 (tasa 0 %) y tampoco lleva
`<IndicadorMontoGravado>`.

**Identificación del comprador** (`EcfDocument.RequiresBuyerIdentification`):
31/41/44/45 siempre (el 45 lo exige el XSD con `minOccurs="1"`; el 44 lo exige la
regla de negocio —el 44 se envía al receptor electrónico— aunque su XSD lo deje
opcional); 32 solo si `MontoTotal ≥ ConsumerIdentificationThreshold`
(DOP 250 000) o comprador extranjero (Identificador Extranjero); 33/34 si su
propio monto llega al umbral o si modifican un e-CF de un tipo que identifica al
comprador (el `NCFModificado` se parsea para saber el tipo). Si una NC/ND modifica
un tipo 32 de monto desconocido, la capa de aplicación lo resuelve con el e-CF
original a la vista.

**Falta (slices posteriores):**

- El formato reducido **RFCE** para el tipo 32 &lt; DOP 250 k (`<RFCE>`, XSD aparte).
- Bloques transversales restantes (ver plan): `Subtotales` + `Paginacion` +
  desglose de `ImpuestosAdicionales` + `Mineria` + `TablaSubcantidad` (PR4).
- El bloque anidado `ImpuestosAdicionalesOtraMoneda` (dentro de `OtraMoneda`) —
  pareado con el desglose ISC de PR4.
- La distribución proporcional de la Sección D a nivel de línea (Formato notas 28/29).
- Sub-tablas de descuento/recargo de línea (`TablaSubDescuento`/`TablaSubRecargo`).
- El **cálculo** de las tasas de retención (hoy los montos los trae el cliente).
- Habilitar `<Retencion>` opcional en los tipos que su XSD lo permite (31/33/34).
- La matriz completa de obligatoriedad 0/1/2/3 por tipo en los validadores por
  tipo (Módulo 12).
- El agregado persistido `Ecf` + tabla `comprobantes_ecf` — llega con Módulo 4.
