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

**Incluido:** tipos **31**, **32**, **33** y **34** con IdDoc, Emisor, Comprador,
Totales, DetallesItems, InformacionReferencia. Descuentos/recargos de línea
(`DescuentoMonto`/`RecargoMonto` directos), múltiples tasas de ITBIS, tipos de item
(bien/servicio), formas de pago.

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

**Verificado contra el XSD**: `IndicadorBienoServicio` es **1 = Bien, 2 = Servicio**
(el contexto viejo decía B/S). `RNCValidationType` son 9 u 11 dígitos (no 10).
`IndicadorFacturacion` admite `0` ("No Facturable"). En los tipos 32/33/34
`RNCComprador` y `RazonSocialComprador` son `minOccurs="0"` (no estructural).

**Identificación del comprador** (`EcfDocument.RequiresBuyerIdentification`):
31/41/44/45 siempre; 32 solo si `MontoTotal ≥ ConsumerIdentificationThreshold`
(DOP 250 000) o comprador extranjero (Identificador Extranjero); 33/34 si su
propio monto llega al umbral o si modifican un e-CF de un tipo que identifica al
comprador (el `NCFModificado` se parsea para saber el tipo). Si una NC/ND modifica
un tipo 32 de monto desconocido, la capa de aplicación lo resuelve con el e-CF
original a la vista.

**Falta (slices posteriores):**

- El formato reducido **RFCE** para el tipo 32 &lt; DOP 250 k (`<RFCE>`, XSD aparte).
- Tipos 41, 43–47 — cada uno con su XSD embebido y sus reglas de obligatoriedad.
- Bloques: `InformacionesAdicionales` (exportación), `Transporte`, `OtraMoneda`,
  `Subtotales`, `DescuentosORecargos` (Sección D), `Paginacion`, el desglose de
  `ImpuestosAdicionales` (ISC), sub-tablas de descuento/recargo, retenciones.
- La matriz completa de obligatoriedad 0/1/2/3 por tipo en los validadores por
  tipo (Módulo 12).
- El agregado persistido `Ecf` + tabla `comprobantes_ecf` — llega con Módulo 4.
