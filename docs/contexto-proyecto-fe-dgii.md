# Contexto del proyecto: Proveedor de Facturación Electrónica (DGII, RD)

Documento de continuidad para pegar en las instrucciones del Project y/o subir a su
base de conocimiento. Resume todo lo discutido hasta ahora para que cualquier chat
nuevo dentro del Project arranque con el contexto completo.

---

## 1. Contexto de negocio

- Se está fundando una empresa de software en República Dominicana, equipo de 4
  personas: Emmanuel (dev full-stack), sus dos encargados de trabajo (BD y Sys), y un
  compañero de BD. Ya en proceso de conseguir el RNC.
- Primer proyecto: convertirse en **proveedor de servicios de facturación electrónica
  (FE)** autorizado por la DGII.
- Plan de producto en dos frentes:
  1. **Servicio de FE como SaaS/API** — motor de e-CF que otras empresas/desarrolladores
     consumen (similar a FacturaYa, MSeller).
  2. **Sistema de facturación completo** para PyMEs, con FE integrada.
- Recomendación dada: **no construir ambos en paralelo con un equipo de 4**. Empezar
  por el servicio de FE (más cercano al expertise técnico del equipo, no requiere
  fuerza de ventas/soporte pesado desde el día uno). El sistema para PyMEs se construye
  después, sobre el mismo núcleo de dominio.
- Contexto regulatorio (agosto 2026): ~190 proveedores autorizados en el listado de
  DGII, obligatoriedad de e-CF para grandes y medianos contribuyentes a partir del
  **1 de noviembre de 2026**. Mercado de FE genérico ya saturado.
- Diferenciación recomendada: robustez técnica (contingencia, locks de secuencia,
  vault de certificados, observabilidad) — capitaliza el expertise previo ECFGateway/
  ReciboCaja en UCATECI.
- Plazo objetivo: ~1 mes para código. Certificación ante DGII corre en paralelo.

## 2. Referencias competitivas revisadas

- **FacturaYa**: idempotencia con `X-Idempotency-Key`, detección de duplicados
  (ventana 10 min), rotación de secuencias con colchón, webhooks HMAC-SHA256, backoff
  2min→10min→30min→2h, PDF carta y POS 80mm, endpoint `by-encf` anti-timeout.
- **MSeller ECF**: multi-tenant explícito, Developer/Community (1,000 req/mes ≈250
  comprobantes gratis) vs Partner Comercial con SLA. Comunidad en Discord.
- Ninguno documenta: locks de secuencia bajo concurrencia, vault de certificados,
  detalle operativo de contingencia — ahí está el espacio de diferenciación.

## 3. Arquitectura propuesta (visión general)

4 capas (Clean Architecture basada en `Ucateci.Templates` clean-api):

1. **Ingesta y validación** — API, auth API Key, idempotencia, validación XSD.
2. **Núcleo de dominio** — reglas e-CF puras, sin deps HTTP/DGII. Domain events
   (`DocumentoFirmado`, `DocumentoAceptado`).
3. **Procesamiento asíncrono** — outbox pattern, firma digital, contingencia, vault.
4. **Entrega y eventos** — webhooks HMAC, PDF/RI, reportes 606/607/IT-1.

### Decisiones técnicas clave

- **Lock de secuencia**: `SELECT … FOR UPDATE` o lock distribuido para evitar NCF
  duplicados bajo concurrencia.
- **Outbox sobre Postgres/SQL Server** (`SKIP LOCKED`) en vez de broker dedicado
  desde el día uno. Migrar cuando el volumen lo justifique.
- **Certificados P12**: vault (KMS/Secrets Manager/Key Vault), nunca en disco ni BD.
  Auditoría de uso y alertas de vencimiento 30-60 días antes.
- **Regla de enrutamiento** tipo+monto en el dominio, no en infraestructura.

## 4. Evaluación del template `Ucateci.Templates` (clean-api)

**Reutilizable sin cambios:**
Separación de capas, `AddResilientHttpClient` con Polly, health checks live/ready,
ErrorOr, FluentValidation, auditoría, borrado lógico, paginación, `TimeProvider`
inyectado, descubrimiento por reflexión de `IUseCase<,>`/`IValidator<T>`,
Testcontainers para integración.

**Hay que construir sobre el template:**

| Necesidad | Falta agregar |
|---|---|
| Procesamiento asíncrono con backoff largo | Background worker/outbox |
| Idempotencia de requests | Middleware + storage de claves |
| Lock de asignación de secuencia | `SELECT … FOR UPDATE` o lock distribuido |
| Multi-tenant (una instancia, muchos RNC) | Interceptor de EF Core por tenant |
| Certificados P12 en vault | Integración Key Vault/Secrets Manager |
| Auth API Key + Secret | Adaptar el hueco de auth (preparado para JWT) |
| Outbox para webhooks confiables | Tabla outbox + worker HMAC + reintento |

## 5. Hallazgos de los documentos oficiales de DGII procesados

### 5.1. Descripción Técnica de Facturación Electrónica (v1.6, jun 2023)

- Envío **asíncrono por contrato**: `POST recepcion` → solo TrackId; estado real vía
  `GET consultaresultado`. Tiempo promedio: 200ms.
- **Token expira cada 1 hora**: `GET semilla` → firmar XML → `POST validarsemilla` →
  token. Cache de token por tenant con renovación proactiva (no esperar el 401).
- **Ruteo tipo+monto**: tipo 32 (Consumo) con monto **<RD$250,000** → servicio
  distinto `fc.dgii.gov.do` endpoint `recepcionfc` ("resumen RFCE"), con su propia
  consulta (`consultarfce`) y QR. Monto **≥RD$250,000** → canal normal `ecf.dgii.gov.do`.
- **`secuenciaUtilizada`**: al rechazar un e-CF, la respuesta indica si el número de
  secuencia puede reutilizarse (firma inválida, RNC no autorizado → no reutilizable;
  otros → sí). La liberación no es automática.
- **Rol de servidor**: cada contribuyente debe exponer sus propios endpoints
  (`/fe/autenticacion`, `/fe/recepcion/api/ecf`, `/fe/aprobacioncomercial/api/ecf`)
  para recibir e-CF de otros contribuyentes (B2B). Como proveedor: hospedar estos
  endpoints en nombre del cliente, registrados en el Directorio de DGII.
- **XML**: no incluir tags vacíos, tabla de caracteres a escapar, firma SHA-256 con
  `preservewhitespace=false`, campo `SN` del certificado debe coincidir con el
  RNC/cédula del firmante — validar ANTES de firmar.
- Servicio de estatus `statusecf.dgii.gov.do` (API key propia) permite consultar
  ventanas de mantenimiento → activar contingencia proactivamente.

### 5.2. Informe Técnico e-CF v1.0 (actualizado marzo 2026)

- **e-NCF**: 13 posiciones — `E` + 2 dígitos tipo + 10 dígitos secuenciales. Vence
  automáticamente el **31 de diciembre del año siguiente** a su autorización.
- **Modelo de operación**: emisor → DGII → TrackId → emisor envía e-CF al receptor →
  receptor acusa recibo → receptor envía Aprobación/Rechazo Comercial (opcional). Si
  receptor no es electrónico: solo entrega RI, él consulta validez en web de DGII y
  reporta compra en Formato 606.
- **Tolerancia y cuadratura**: ±1 unidad por línea, tolerancia global = cantidad de
  líneas. Si se excede → **no rechaza, acepta condicional**. No rechazar localmente.
- **Redondeo**: estándar 2 decimales EXCEPTO `PrecioUnitarioItem`/`PrecioUnitarioItemOtraMoneda`
  y `TipoCambio` (4 decimales), `Subcantidad` (3 decimales).
- **ISC (alcoholes y cigarrillos)** y otros impuestos adicionales: fórmulas específicas.
  Fuera del day-one salvo que un cliente temprano opere en esos rubros.
- **Máquina de estados**: receptor rechaza → emisor anula con nota de crédito.
  Receptor aprueba PERO DGII rechaza → e-CF y aprobación se invalidan igual; emitir e-CF
  nuevo. "Aprobado por receptor" ≠ válido.
- **RI**: máx 1,000 líneas (10,000 para tipo 32 <250k). QR con: `RncEmisor`, `ENCF`,
  `RncComprador`, `FechaEmision`, `MontoTotal`, `FechaFirma`, `CodigoSeguridad` (primeros
  6 dígitos del hash del `SignatureValue`). URL diferente para RFCE.
- **Contingencia — tres tipos con relojes distintos:**
  1. Falta de conectividad: generar offline, remitir en **72 horas** de restablecida.
     RI lleva leyenda *"e-CF emitido en modalidad de contingencia"*. Solo válido
     fiscalmente después del plazo.
  2. Imposibilidad técnica de emitir: usar NCF de papel máx **15 días calendario**,
     regularizar en **30 días** con e-CF referenciando los NCF de papel.
  3. Contingencia de la propia DGII: almacenar y remitir; si >15 días hábiles,
     Oficina Virtual habilita reportes alternativos.

### 5.3. Formato Comprobante Fiscal Electrónico (e-CF) V1.0 — octubre 2025

**Documento más importante para el modelo de dominio. 87 páginas. Procesado completo.**

#### Estructura general del XML (8 secciones)

```
A. Encabezado          — ~137 campos (emisor, comprador, totales, moneda)
B. Detalle Bienes/Svcs — hasta 39 campos por línea; hasta 1,000 líneas (10,000 tipo 32 <250k)
C. Subtotales          — 0-20 grupos informativos (no afectan base imponible)
D. Descuentos/Recargos — 0-20 líneas globales (SÍ afectan base imponible)
E. Paginación          — condicional si >1 página; 1-100 páginas
F. Info de Referencia  — máx 1 línea (para NC, ND, anulaciones, reemplazos)
G. Fecha/Hora Firma    — dd-MM-AAAA HH:mm:ss, GMT-4
H. Firma Digital       — XMLDSig sobre todas las secciones anteriores
```

**Obligatoriedad por columna**: 0=no corresponde, 1=obligatorio, 2=condicional, 3=opcional.
**Columna RI (I)**: N=no obligatorio imprimir, I=obligatorio imprimir, P=imprimir en palabras.

#### Campos clave del Encabezado (hallazgos críticos)

| Campo XML | Tipos donde aplica | Regla crítica |
|---|---|---|
| `<FechaVencimientoSecuencia>` | 31,33,41,43,44,45,46,47 | **Tipos 32 y 34 → obligatoriedad=0 (no corresponde)** |
| `<IndicadorNotaCredito>` | Solo 34 | 0=dentro de 30 días (tiene derecho a devolución ITBIS); 1=después de 30 días |
| `<IndicadorEnvioDiferido>` | Condicional | Solo para contribuyentes móviles/offline autorizados |
| `<IndicadorMontoGravado>` | Todos | 0=ITBIS NO incluido en precio; 1=ITBIS incluido (hay que extraer la base) |
| `<RNCComprador>` | Tipo 32 | Solo requerido si monto ≥ RD$250,000; también para extranjeros con `<IdentificadorExtranjero>` |
| `<MontoTotal>` | Todos | Para tipo 34 (Nota de Crédito): **debe ser ≤ MontoTotal del e-CF modificado** |
| `<TipoCambio>` | OtraMoneda | 4 decimales (confirmado) |
| `<TipoMoneda>` | OtraMoneda | Códigos ISO de Tabla II |
| `<TipoPago>` | Todos | Condicional — ver `<TablaFormasPago>` |
| `<FechaLimitePago>` | Tipos crédito | Obligatoriedad=2 cuando hay crédito |
| `<TipoIngresos>` | 31,33,34,41,45,46,47 | Clasifica el ingreso para reportes |

#### Encabezado — área Totales (campos 69-119 aprox.)

Campos totalizadores del Encabezado — todos de 18 chars NUM, 2 decimales:

- `<MontoGravadoTotal>` — suma ITBIS tasa 1+2+3 gravado
- `<MontoGravadoI1>` — gravado a 18%
- `<MontoGravadoI2>` — gravado a 16%
- `<MontoGravadoI3>` — gravado a 0%
- `<ITBIS1>`, `<ITBIS2>`, `<ITBIS3>` — montos de ITBIS por tasa
- `<TotalITBISRetenido>` — retenciones de ITBIS
- `<TotalISRRetenido>` — retenciones de ISR
- `<MontoExento>` — total exento
- `<TotalImpuestosAdicionales>` — Propina Legal + CDT + ISC servicios + Primera Placa
- `<TotalImpuestoSelectivoConsumo>` — ISC alcoholes y cigarrillos (tipos 006-039)
  - **CRÍTICO**: el ISC selectivo al consumo (códigos 006-039) forma parte de la
    **base imponible del ITBIS** — se suma al precio antes de calcular el ITBIS.
- `<MontoTotal>` — suma de todo; para tipo 34 ≤ MontoTotal del e-CF modificado

#### Encabezado — sección OtraMoneda (campos 120-137)

Activa cuando hay facturación en moneda extranjera. Campos paralelos a los totales
en DOP, todos condicionales (obligatoriedad=2 para la mayoría):

- `<TipoMoneda>` — código ISO Tabla II
- `<TipoCambio>` — 4 decimales, ≥0
- `<MontoGravadoTotalOtraMoneda>`, `<MontoGravadoI1OtraMoneda>`, etc.
- `<MontoTotalOtraMoneda>` — monto total en la moneda extranjera

#### Detalle de Bienes o Servicios — campos completos (39 campos por línea)

| # | Campo XML | Tipo | Largo | Regla clave |
|---|---|---|---|---|
| 1 | `<NumeroLinea>` | NUM | 5 | 1 a 10,000 (tipo 32 <250k) ó 1 a 1,000 |
| 2 | `<TablaCodigosItem>` | Tabla | — | Pares Tipo/Código (hasta ~repeticiones) |
| 3 | `<IndicadorFacturacion>` | NUM | 1 | 1=ITBIS 18%, 2=16%, 3=0%, 4=Exento |
| 4 | Área Retención | — | — | ISR/ITBIS retenido en el ítem |
| 5 | `<NombreItem>` | ALFA | 80 | **I=1 (obligatorio imprimir) para TODOS los tipos** |
| 6 | `<IndicadorBienServicio>` | ALFA | 1 | B=Bien, S=Servicio |
| 7 | `<DescripcionItem>` | ALFA | 240 | Descripción larga opcional |
| 8 | `<CantidadItem>` | NUM | 16+2d | Cantidad del ítem |
| 9 | `<UnidadMedida>` | ALFA | — | Código Tabla IV (ver sección de tablas) |
| 10 | `<CantidadReferencia>` | NUM | — | Para conversiones de unidad |
| 11 | `<UnidadReferencia>` | ALFA | — | Unidad de referencia |
| 12 | `<TablaSubcantidad>` | Tabla | — | Subcantidades por componente |
| 13 | `<Subcantidad>` | NUM | — | **3 decimales** (≠ regla general de 2) |
| 14 | `<GradosAlcohol>` | NUM | — | Solo alcoholes |
| 15 | `<PrecioUnitarioReferencia>` | NUM | — | Precio en unidad de referencia |
| 16 | `<FechaElaboracion>` | ALFA | 10 | dd-MM-AAAA |
| 17 | `<FechaVencimientoItem>` | ALFA | 10 | dd-MM-AAAA, opcional mayoría tipos |
| 18 | Área Minería | — | — | Solo sector minero |
| 21 | `<PesoNetoKilogramo>` | NUM | 19+3d | Minería: peso neto kg |
| 22 | `<PesoNetoMineria>` | NUM | 19+3d | Minería: peso neto mineral |
| 23 | `<TipoAfiliacion>` | NUM | 1 | 1=Afiliada, 2=No afiliada |
| 24 | `<Liquidacion>` | NUM | 1 | 1=Provisional, 2=Final |
| 25 | `<PrecioUnitarioItem>` | NUM | 20+4d | **I=1 TODOS los tipos; 4 decimales** |
| 26 | `<DescuentoMonto>` | NUM | 18+2d | Suma de subdescuentos del ítem |
| 27-29 | `<TablaSubDescuento>` | Tabla | — | **Hasta 12 pares** (Tipo "$"/"%" + % + Monto) |
| 30 | `<RecargoMonto>` | NUM | 18+2d | Suma de subrecargos del ítem |
| 31-33 | `<TablaSubRecargo>` | Tabla | — | **Hasta 12 pares** (Tipo + % + Monto) |
| 34 | `<TablaImpuestosAdicionales>` | Tabla | — | **Hasta 2 repeticiones** de código impuesto |
| 34b | `<CódigoImpuestoAdicional>` | NUM | 3 | Código de Tabla I |
| 35 | `<PrecioOtraMoneda>` | NUM | 20+4d | Precio unitario en moneda extranjera |
| 36 | `<DescuentoOtraMoneda>` | NUM | 18+2d | Descuento en moneda extranjera |
| 37 | `<RecargoOtraMoneda>` | NUM | 18+2d | Recargo en moneda extranjera |
| 38 | `<MontoItemOtraMoneda>` | NUM | 18+2d | (Precio*Cant)-Desc+Rec en moneda extranjera |
| 39 | `<MontoItem>` | NUM | 18+2d | **I=1 TODOS los tipos**; puede ser 0 si NC corrección texto (CodigoModificacion=2) |

**Fórmula `<MontoItem>`**: (PrecioUnitarioItem × CantidadItem) − DescuentoMonto + RecargoMonto

#### Subtotales Informativos (Sección C)

- 0 a **20 grupos** de subtotal (todos opcionales, obligatoriedad=3).
- **NO aumentan/disminuyen la base imponible** — solo informativos.
- Cada subtotal `<Subtotal>` tiene: `<NumeroSubTotal>`, `<DescripcionSubtotal>`,
  `<Orden>` (para impresión), y subtotales por tasa ITBIS:
  `<SubTotalMontoGravadoTotal>`, `<SubTotalMontoGravadoI1/2/3>`,
  `<SubTotalITBIS>`, `<SubTotalITBIS1/2/3>`, `<SubTotalImpuestoAdicional>`,
  `<SubTotalExento>`, `<MontoSubTotal>`, `<Lineas>`.

#### Descuentos o Recargos (Sección D)

- 0 a **20 líneas** globales. **SÍ afectan la base imponible**.
- **Regla crítica para implementación**: si el Detalle contiene ítems con
  distintos códigos de impuesto, el `<TipoValor>` del descuento global DEBE
  ser `%` (porcentaje), no monto fijo `$`.
- Campos por línea:
  - `<NumeroLinea>` o `<NúmeroSecuencial>` — secuencial hasta 20
  - `<TipoAjuste>` — `D`(escuento) o `R`(ecargo)
  - `<IndicadorNorma1007>` — valor 1 si aplica Norma 10-07; solo tipo 31, obligatoriedad=3
  - `<DescripcionDescuentooRecargo>` — 45 chars, **I=1** para tipos que aplica
  - `<TipoValor>` — `%` o `$`
  - `<ValorDescuentooRecargo>` — valor % si aplica
  - `<MontoDescuentooRecargo>` — monto si aplica
  - `<MontoDescuentooRecargoOtraMoneda>` — en moneda extranjera
  - `<IndicadorFacturacionDescuentooRecargo>` — 1=ITBIS1(18%), 2=ITBIS2(16%),
    3=ITBIS3(0%), 4=Exento(E); indica a qué tasa aplica el descuento global.
    Condicional a que exista descuento o recargo global.

#### Paginación (Sección E)

Condicional cuando el e-CF tiene >1 página. `<Pagina>` se repite para el total de
páginas especificadas (campo `<TotalPaginas>` del Encabezado). Campos por página:

- `<PaginaNo>` — **I=1** para todos, entre 1 y 100
- `<NoLineaDesde>`, `<NoLineaHasta>` — primera/última línea en la página
- `<SubtotalMontoGravadoPagina>` (Total Página) — **I=1** (excepto 43,45,47=0)
- `<SubtotalMontoGravado1/2/3Pagina>` — por tasa ITBIS
- `<SubtotalExentoPagina>` — exento de la página
- `<SubtotalItbisPagina>`, `<SubtotalITBIS1/2/3Pagina>` — ITBIS totales
- `<SubtotalImpuestoAdicionalPagina>` — impuestos adicionales (sin ISC)
- `<SubtotalImpuestoSelectivoConsumoPagina>` — ISC de la página
- `<SubtotalOtrosImpuestoPagina>` — otros impuestos adicionales de la página
- `<MontoSubtotalPagina>` — **I=1**; suma de gravado + exento + ITBIS + adicionales
- `<SubtotalMontoNoFacturablePagina>` — condicional

#### Información de Referencia (Sección F)

Máx 1 línea. Obligatoriedad de la sección: 33=1, 34=1; 31,32,41-47=2.

| Campo | Largo | Regla |
|---|---|---|
| `<NCFModificado>` | 11-13 | Puede ser e-NCF (E+tipo+10dig) o NCF papel (A/B+...); debe haber sido enviado a DGII previamente; I=1 |
| `<RNCOtroContribuyente>` | 9-11 | Solo si el RNC del emisor del e-CF no coincide con el del NCF modificado (por baja, fusión, escisión) |
| `<FechaNCFModificado>` | 10 | dd-MM-AAAA; fecha de emisión del comprobante modificado |
| `<CodigoModificacion>` | 1 | P (imprimir en palabras); valores: 1=Anula NCF, 2=Corrige Texto, 3=Corrige Montos, 4=Reemplazo contingencia, 5=Referencia RFCE (solo tipo 31) |
| `<RazonModificacion>` | 90 | ALFA; opcional (3); solo para NC/ND; ejemplo: "error en precio" |

**`<CodigoModificacion>` detalle:**
- Códigos 1, 2, 3 solo aplican para Nota de Crédito o Débito (tipos 33, 34).
- Código 4: reemplazo de e-CF emitido en contingencia — el tipo del NCF modificado
  debe coincidir con el tipo del e-CF que se está emitiendo.
- Código 5: solo para tipo 31 (Crédito Fiscal), referencia a una Factura de Consumo
  Electrónica (RFCE).
- Cuando `CodigoModificacion=2` (corrección de texto), `<MontoItem>` puede ser 0
  en líneas de detalle — se permite imprimir texto explicativo sin valor.

#### Fecha y Hora de la Firma Digital (Sección G)

- `<FechaHoraFirma>` — 19 chars, ALFA/NUM, formato `dd-MM-AAAA HH:mm:ss`, zona
  horaria **GMT-4**. Validación: fecha/hora válida **y ≤ fecha/hora actual del sistema**.

#### Firma Digital (Sección H)

- `<Signature>` — obligatoriedad=1 para TODOS los tipos.
- Cubre: Encabezado + Detalle + Descuentos/Recargos + Paginación +
  Información de Referencia + Fecha y Hora de Firma.
- XML-DSig, SHA-256, `preservewhitespace=false` (confirmado de sección 5.1).

#### TABLA I — Codificación Tipos de Impuestos Adicionales

| Código | Abreviatura | Descripción | Tasa |
|---|---|---|---|
| 001 | Propina Legal | Propina Legal | 10% |
| 002 | CDT | Contribución Desarrollo Telecom (Ley 153-98 Art.45) | 2% |
| 003 | ISC | Servicios Seguros en general | 16% |
| 004 | ISC | Servicios de Telecomunicaciones | 10% |
| 005 | — | Expedición Primera Placa (Registro Vehículos) | 17% |
| 006-018 | ISC Específico | Alcoholes: Cerveza, Vinos, Vodka, Whisky, Ron, etc. | 632.58 DOP/unidad* |
| 019-022 | ISC Específico | Cigarrillos (cajetilla 20u / 10u) | 53.51 / 26.75 DOP* |
| 023-035 | ISC AdValorem | Mismos productos alcoholes | 10% |
| 036-039 | ISC AdValorem | Cigarrillos | 20% |

*Los montos específicos (códigos 006-039) se ajustan trimestralmente conforme al
índice de inflación del Banco Central.

**Regla crítica ISC**: los impuestos selectivos al consumo específicos (códigos 006-039)
**integran la base imponible del ITBIS**. El flujo de cálculo es:
Precio base → +ISC específico → base imponible ITBIS → ×tasa ITBIS = ITBIS a pagar.

#### TABLA II — Codificación Monedas (ISO)

BRL, CAD, CHF, CHY, XDR, DKK, EUR, GBP, JPY, NOK, SCP, SEK, USD, VEF, HTG, MXN,
COP (Peso Colombiano — añadido en la actualización de oct-2025 del documento).

#### TABLA III — Provincias y Municipios

32 provincias de la RD con sus municipios y distritos municipales. Fuente: ONE,
actualizada al 30 junio 2014. Código de provincia (6 dígitos) + código de municipio.

#### TABLA IV — Unidades de Medida (57 unidades)

BARR, BOL, BOT, BULTO, BOTELLA, CAJ, CAJETILLA, CM, CIL, CONJ, CONT, DÍA, DOC,
FARD, GL, GRAD, GR, GRAN, HOR, HUAC, KG, kWh, LB, LITRO, LOT, M, M², M³, MMBTU,
MIN, PAQ, PAR, PIE, PZA, ROL, SOBR, SEG, TANQUE, TONE, TUB, YD, YD², UND, EA,
MILLAR, SAC, LAT, DIS, BID, RAC, Q, GRT, P2, PAX, PULG, STAY, BDJ.

#### Resumen de hallazgos críticos del Formato e-CF para el modelo de dominio

1. **Tipos 32 y 34 NO tienen `<FechaVencimientoSecuencia>`** (obligatoriedad=0). El
   modelo de dominio no debe requerir ese campo para esos tipos.
2. **`<PrecioUnitarioItem>` es 4 decimales y obligatorio (I=1) para todos los tipos**,
   confirmando la regla de redondeo diferenciada del Informe Técnico.
3. **`<MontoItem>` es obligatorio (I=1) para todos los tipos**, pero puede ser 0 cuando
   la Nota de Crédito tiene `CodigoModificacion=2` (corrección de texto).
4. **Subcantidad usa 3 decimales**, no 2 — regla de excepción de redondeo confirmada.
5. **`TipoCambio` usa 4 decimales** — confirmado como excepción a la regla general.
6. **Para tipo 32 <250k, el límite de líneas es 10,000**; para todos los demás, 1,000.
7. **Los descuentos globales (Sección D) con ítems de distintos tipos de ITBIS deben
   ser en %** — si el sistema permite descuentos en monto fijo, hay que validar esto.
8. **`<CodigoModificacion>=5`** (Referencia RFCE) es exclusivo de tipo 31 (Crédito
   Fiscal), no de tipos 33/34.
9. **`<NCFModificado>` puede ser tanto e-NCF electrónico como NCF de papel** — el
   validador debe aceptar ambos formatos.
10. **`<FechaHoraFirma>` debe ser GMT-4 y ≤ fecha/hora actual** — importante para
    sistemas en servidores con timezone distinto.
11. **La firma cubre todas las secciones A-G** — cualquier modificación post-firma
    invalida el documento completo.
12. **ISC específico (006-039) es parte de la base imponible del ITBIS** — el motor de
    cálculo debe aplicar ISC antes de calcular el ITBIS, no después.
13. **`<RNCComprador>` en tipo 32 solo es obligatorio si monto ≥ RD$250,000** — no
    requerirlo en consumidores finales de bajo monto.
14. **Paginación**: el campo `<PaginaNo>` va de 1 a 100 — máximo 100 páginas por e-CF.
15. **`<IndicadorMontoGravado>=1`** requiere extraer la base imponible del precio
    (precio incluye ITBIS) — lógica inversa de cálculo.

### 5.4 Formato RFCE v1.0 — Resumen Factura Consumo Electrónica <DOP250,000 (Enero 2020)

**Propósito:** XML de resumen que el emisor envía a DGII por cada Factura de Consumo
Electrónica (tipo 32) **menor a DOP$250,000**. No sustituye al e-CF entregado al comprador
(que sigue el formato completo de la sección 5.3); es un resumen paralelo enviado al endpoint
RFCE de DGII.

**Estructura — 2 secciones únicamente:**
- A. Encabezado (obligatoriedad=1)
- B. Firma Digital (obligatoriedad=1) — sobre todo el documento

#### Campos del Encabezado — tabla completa (31 campos)

| # | Tag XML | Tipo | Largo | Oblig. | Regla / Validación clave |
|---|---------|------|-------|--------|--------------------------|
| 1 | `<Version>` | ALFANUM | 3 | 1 | Valor fijo: 1.0 |
| — | `<IdDoc>` | — | — | 1 | Bloque identificación del documento |
| 2 | `<TipoCF>` | NUM | 2 | 1 | Código 32 (Factura de Consumo Electrónica) |
| 3 | `<eNCF>` | ALFANUM | 13 | 1 | Secuencia autorizada DGII, formato e-NCF |
| 4 | `<TipoIngresos>` | NUM | 2 | 1 | **Campo exclusivo RFCE**: 01=Operaciones no financieras, 02=Financieros, 03=Extraordinarios, 04=Arrendamientos, 05=Venta Activo Depreciable, 06=Otros |
| 5 | `<TipoPago>` | NUM | 1 | 1 | 1=Contado, 2=Crédito, 3=Gratuito |
| — | `<TablaFormasPago>` | — | — | 3 | Hasta 7 repeticiones de pares FormaPago+MontoPago; opcional |
| 6 | `<FormaPago>` | NUM | 2 | 3 | 1=Efectivo, 2=Cheque/Transferencia/Depósito, 3=Tarjeta Débito/Crédito, 4=Venta a Crédito, 5=Bonos/Certificados, 6=Permuta, 7=Nota de crédito, 8=Otras |
| 7 | `<MontoPago>` | NUM | 18 | 2 | 16 enteros, 2 dec; ≥0; condicional a FormaPago |
| — | `<Emisor>` | — | — | 1 | Bloque emisor |
| 8 | `<RNCEmisor>` | NUM | 9-11 | 1 | RNC activo, autorizado como Facturador Electrónico, sin bloqueos |
| 9 | `<RazonSocialEmisor>` | ALFANUM | 150 | 1 | Sin validación |
| 10 | `<FechaEmision>` | ALFANUM | 10 | 1 | Formato dd-MM-AAAA; validar fecha inicio como facturador |
| — | `<Comprador>` | — | — | 3 | **Sección Comprador COMPLETA es opcional (=3)** para tipo 32 <250k |
| 11 | `<RNCComprador>` | NUM | 9-11 | 3 | Sin validación adicional |
| 12 | `<IdentificadorExtranjero>` | ALFANUM | 20 | 2 | Condicional a comprador extranjero; **si se usa, RNCComprador debe ir en blanco** |
| 13 | `<RazonSocialComprador>` | ALFANUM | 150 | 3 | Sin validación |
| — | `<Totales>` | — | — | 1 | Bloque de totales |
| 14 | `<MontoGravadoTotal>` | NUM | 18 | 2 | 16 enteros, 2 dec; ≥0; suma MontoGravadoI1+I2+I3 |
| 15 | `<MontoGravadoI1>` | NUM | 18 | 2 | Base gravable ITBIS 18%; condicional a ítem al 18% |
| 16 | `<MontoGravadoI2>` | NUM | 18 | 2 | Base gravable ITBIS 16%; condicional a ítem al 16% |
| 17 | `<MontoGravadoI3>` | NUM | 18 | 2 | Base gravable ITBIS 0%; condicional a ítem al 0% |
| 18 | `<MontoExento>` | NUM | 18 | 2 | Condicional a ítem exento |
| 19 | `<TotalITBIS>` | NUM | 18 | 2 | Suma TotalITBIS1+2+3; condicional a montos ITBIS declarados |
| 20 | `<TotalITBIS1>` | NUM | 18 | 2 | ITBIS al 18% |
| 21 | `<TotalITBIS2>` | NUM | 18 | 2 | ITBIS al 16% |
| 22 | `<TotalITBIS3>` | NUM | 18 | 2 | ITBIS al 0% |
| 23 | `<MontoImpuestoAdicional>` | NUM | 18 | 2 | Suma ISC Específico + ISC AdValorem + Otros; condicional a ISC |
| — | `<ImpuestosAdicionales>` | — | — | 2 | Hasta 20 repeticiones; mismos 4 sub-campos que e-CF; validar con Tabla I |
| 24 | `<TipoImpuesto>` | NUM | 3 | 2 | Validar con Tabla I del Formato e-CF |
| 25 | `<MontoImpuestoSelectivoConsumoEspecifico>` | NUM | 18 | 2 | >0 |
| 26 | `<MontoImpuestoSelectivoConsumoAdvalorem>` | NUM | 18 | 2 | >0 |
| 27 | `<OtrosImpuestosAdicionales>` | NUM | 18 | 2 | >0 |
| 28 | `<MontoTotal>` | NUM | 18 | 1 | **Obligatorio=1**; = MontoGravadoTotal + MontoExento + TotalITBIS + MontoImpuestoAdicional |
| 29 | `<MontoNoFacturable>` | NUM | 18 | 2 | **Puede ser negativo** — excepción a regla general |
| 30 | `<MontoPeriodo>` | NUM | 18 | 3 | **Puede ser negativo**; = MontoTotal + MontoNoFacturable |
| 31 | `<CodigoSeguridadCF>` | ALFANUM | 6 | 1 | **6 primeros caracteres del Hash de la firma digital del e-CF emitido <DOP$250M** — vínculo con el e-CF correspondiente |

**Regla de redondeo (nota al pie del PDF):** En campos de 16 enteros y 2 decimales,
aplicar regla de redondeos según el Informe Técnico de e-CF.

#### Hallazgos críticos del RFCE para el modelo de dominio

1. `<TipoIngresos>` es un campo **exclusivo del RFCE** (no existe en el e-CF). Necesita
   captura en el formulario de emisión de factura de consumo <250k.
2. La sección `<Comprador>` en el RFCE es completamente **opcional** — consistente con
   la regla del e-CF de que RNCComprador solo es requerido si monto ≥ DOP$250,000.
3. `<CodigoSeguridadCF>` = primeros 6 chars del hash de firma — permite a DGII
   correlacionar el RFCE con el e-CF real sin necesidad de re-enviar el documento completo.
4. `<MontoNoFacturable>` y `<MontoPeriodo>` pueden ser **negativos** — el tipo de dato
   debe ser signed decimal, no unsigned.
5. La `<TablaFormasPago>` tiene **hasta 7 repeticiones** (no 20 como otras tablas).
6. `<IdentificadorExtranjero>` es mutuamente excluyente con `<RNCComprador>` — si uno
   está presente, el otro debe estar en blanco.

---

### 5.5 Formato ARECF v1.0 — Acuse de Recibo (sin fecha, versión 1.0)

**Propósito:** Respuesta que el **receptor** (comprador) envía al **emisor** como
constancia de recepción del e-CF. El ARECF **no implica aceptación ni rechazo** — solo
indica si el documento fue o no recibido. El receptor está **obligado** a enviar el ARECF
previo a la aprobación comercial.

**Estructura — 2 secciones:**
- A. Detalle Acuse de Recibo (obligatoriedad=1)
- B. Firma Digital (obligatoriedad=1) — sobre el archivo de Acuse de Recibo

#### Campos — Sección A `<DetalleAcusedeRecibo>`

| # | Tag XML | Tipo | Largo | Oblig. | Regla / Validación clave |
|---|---------|------|-------|--------|--------------------------|
| 1 | `<Version>` | NUM | 3 | 1 | Valor: 1.0 (**NUM** — diferente al e-CF que usa ALFANUM) |
| 2 | `<RNCEmisor>` | NUM | 9-11 | 1 | RNC del emisor del e-CF (el que emitió la factura); formato correcto |
| 3 | `<RNCComprador>` | NUM | 9-11 | 1 | RNC del comprador (quien emite el acuse de recibo); formato correcto |
| 4 | `<eNCF>` | ALFANUM | 13 | 1 | e-NCF del comprobante recibido; estructura válida |
| 5 | `<Estado>` | NUM | 1 | 1 | **0=e-CF Recibido**, 1=e-CF No Recibido |
| 6 | `<CodigoMotivoNoRecibido>` | NUM | 1 | 2 | Condicional a Estado=1; códigos: **1=Error de especificación, 2=Error de Firma Digital, 3=Envío duplicado, 4=RNC Comprador no corresponde** |
| 7 | `<FechaHoraAcuseRecibo>` | ALFANUM | 19 | 1 | Formato dd-MM-AAAA HH:mm:ss |

**Bitácora v1.0:** Se actualizó la etiqueta del área de 'DETALLE ACUSE DE RECIBO'.

#### Hallazgos críticos del ARECF para el modelo de dominio

1. El ARECF es **obligatorio** — el receptor está legalmente obligado a enviar acuse antes
   de la aprobación comercial. El sistema debe implementar recepción de ARECF entrantes
   (de terceros que reciben nuestros e-CFs) y emisión de ARECF salientes (cuando somos
   receptores de e-CFs de terceros).
2. `<RNCEmisor>` en el ARECF = RNC de quien emitió la **factura original**; `<RNCComprador>`
   = RNC de quien **emite el acuse** (los roles se invierten respecto al flujo de facturación).
3. `<Version>` es tipo **NUM** (no ALFANUM) — el serializador ARECF debe tratar este campo
   diferente al de e-CF.
4. Los 4 motivos de no-recibo (Error especificación, Error firma, Duplicado, RNC no
   corresponde) deben modelarse como enum con códigos 1-4.
5. El campo `<CodigoMotivoNoRecibido>` aparece solo si Estado=1 (No Recibido) — obligatorio
   condicional, no opcional.

---

### 5.6 Formato ACECF v1.0 — Aprobación Comercial (Enero 2020)

**Propósito:** Respuesta que el **comprador** envía al **emisor** y copia a DGII indicando
aceptación o rechazo comercial del e-CF. La aprobación comercial es **OPCIONAL** — el
comprador "podrá remitir" (no está obligado), a diferencia del ARECF que sí es obligatorio.
La DGII solo recibe ACECF de e-CFs previamente aceptados por ella.

**Estructura — 2 secciones:**
- A. Detalle Aprobación Comercial (obligatoriedad=1)
- B. Firma Digital (obligatoriedad=1) — sobre el archivo de Aprobación Comercial

#### Campos — Sección A `<DetalleAprobacionComercial>`

| # | Tag XML | Tipo | Largo | Oblig. | Regla / Validación clave |
|---|---------|------|-------|--------|--------------------------|
| 1 | `<Version>` | NUM | 3 | 1 | Valor: 1.0 |
| 2 | `<RNCEmisor>` | NUM | 9-11 | 1 | RNC del emisor del e-CF; **debe coincidir con RNC del e-CF original** |
| 3 | `<eNCF>` | ALFANUM | 13 | 1 | **Debe coincidir con el e-NCF del e-CF remitido** |
| 4 | `<FechaEmision>` | ALFANUM | 10 | 1 | Formato dd-MM-AAAA; **debe coincidir con FechaEmision del e-CF original** |
| 5 | `<MontoTotal>` | NUM | 18 | 1 | **Debe coincidir exactamente con MontoTotal del e-CF emitido** |
| 6 | `<RNCComprador>` | NUM | 9-11 | 1 | **Debe coincidir con RNCComprador del e-CF original** |
| 7 | `<Estado>` | NUM | 1 | 1 | **1=e-CF Aceptado, 2=e-CF Rechazado** |
| 8 | `<DetalleMotivoRechazo>` | ALFANUM | 250 | 2 | Texto libre; condicional a Estado=2 |
| 9 | `<FechaHoraAprobacionComercial>` | ALFANUM | 19 | 1 | Formato dd-MM-AAAA HH:mm:ss |

#### Hallazgos críticos del ACECF para el modelo de dominio

1. ACECF es **opcional** (ARECF es obligatorio, ACECF no). El sistema debe poder procesarlo
   cuando llegue, pero no bloquearse si no llega.
2. Todos los campos de referencia (`RNCEmisor`, `eNCF`, `FechaEmision`, `MontoTotal`,
   `RNCComprador`) deben ser **validados cruzadamente** contra el e-CF original en base de
   datos. Una discrepancia en cualquiera invalida el ACECF.
3. `<Estado>` tiene solo 2 valores: 1=Aceptado, 2=Rechazado. No hay "pendiente" — el estado
   pendiente es simplemente la ausencia de ACECF.
4. `<DetalleMotivoRechazo>` hasta 250 chars de texto libre — en la UI de gestión de e-CFs
   esto debe mostrarse claramente cuando Estado=2.
5. El tipo de e-CF que puede recibir ACECF incluye tipo 45 (Gubernamental) según el
   texto introductorio — esto resuelve la **inconsistencia #5** del plan técnico previo.
6. La DGII debe recibir copia del ACECF — implica que el sistema necesita enviar el ACECF
   tanto al endpoint B2B del emisor **como** al endpoint de DGII.

---

### 5.7 Formato ANECF v1.0 — Anulación de e-NCF (Mayo 2022)

**Propósito:** XML que permite al contribuyente anular **secuencias autorizadas no utilizadas**
de comprobantes fiscales electrónicos. **Solo aplica** cuando la factura NO fue enviada a
DGII ni al receptor, o la secuencia no fue utilizada. Si ya fue enviada → se emite Nota
de Crédito (tipo 34), no ANECF.

**Versión 1.0 — Mayo 2022. Actualización 24-05-2022:** Se eliminó la validación que
prohibía anular secuencias ya cubiertas por un ANECF previo (ahora sí se permiten rangos
que se solapan con anulaciones anteriores).

**Estructura — 3 secciones (todas obligatoriedad=1):**
- a. Encabezado
- b. Detalle de Anulación
- c. Firma Digital

**Solo 2 códigos de obligatoriedad en ANECF:** 0=No corresponde (no debe aparecer) y
1=Obligatorio. No hay condicional (2) ni opcional (3).

#### Sección a. Encabezado `<Encabezado>`

| # | Tag XML | Tipo | Largo | Oblig. | Regla |
|---|---------|------|-------|--------|-------|
| 1 | `<Version>` | NUM | 3 | 1 | Valor: 1.0 |
| 2 | `<RncEmisor>` | NUM | 9-11 | 1 | RNC activo, autorizado como Facturador Electrónico, **con secuencias autorizadas del tipo a anular** |
| 3 | `<CantidadeNCFAnulados>` | NUM | 10 | 1 | Suma TOTAL de todos los e-NCF anulados en toda la sección Detalle |
| 4 | `<FechaHoraAnulacioneNCF>` | ALFANUM | 19 | 1 | Fecha/hora generación del archivo; formato dd-MM-AAAA HH:mm:ss |

#### Sección b. Detalle de Anulación `<DetalleAnulacion>`

Estructura anidada en **3 niveles**:

```
<DetalleAnulacion>                 — obligatorio
  <Anulacion> ×1..8               — hasta 8 repeticiones (una por tipo de e-CF)
    <NoLinea>                      — NUM 2, desde 1 hasta 10
    <TipoCF>                       — NUM 2; los 10 tipos: 31,32,33,34,41,43,44,45,46,47
    <TablaRangoSecuenciasAnuladasNCF> ×1..10,000  — hasta 10,000 rangos por tipo
      <SecuenciaeNCFDesde>         — ALFANUM 13; inicio del rango
      <SecuenciaeNCFHasta>         — ALFANUM 13; fin del rango (≥ Desde)
    <CantidadeNCFAnulados>         — NUM 10; Σ secuencias dentro de este bloque de tipo
```

**Validaciones de `<SecuenciaeNCFDesde>` y `<SecuenciaeNCFHasta>`:**
- e-NCF formato correcto (13 chars): serie E-Z (excluye P) + tipo 2 dig + secuencial 10 dig
- Tipo del e-NCF debe corresponder al `<TipoCF>` del bloque
- `Desde ≤ Hasta` y ambos > 0
- Desde y Hasta deben tener la misma serie y tipo
- Un rango de un solo e-NCF tiene Desde = Hasta

**Ejemplo del Anexo I** (confirmado en página 9 del PDF):
- Línea 1: TipoCF=31; 2 rangos → E310000000001–E310000000001 (1 e-NCF) + E310000000005–E310000000050 (46 e-NCF) = 47 anulados
- Línea 2: TipoCF=44; 1 rango → E440000000010–E440000000046 = 37 anulados
- Total en Encabezado: CantidadeNCFAnulados = 84

#### Sección c. Firma Digital

`<Signature>` — firma sobre el archivo de Anulación de e-NCF, obligatorio=1.

#### Hallazgos críticos del ANECF para el modelo de dominio

1. **ANECF vs Nota de Crédito:** La lógica de negocio debe verificar si el e-CF fue enviado
   antes de decidir qué mecanismo usar. Enviado → Nota de Crédito (tipo 34). No enviado /
   no utilizado → ANECF. Esta bifurcación es crítica.
2. La estructura permite cancelar hasta **8 tipos distintos** en un solo ANECF, y hasta
   **10,000 rangos de secuencias por tipo** — diseñado para escenarios de migración masiva
   o purgas de secuencias expiradas.
3. `<CantidadeNCFAnulados>` existe en DOS niveles: en el Encabezado (total global) y en
   cada bloque de Anulación (subtotal por tipo). El sistema debe calcular y mantener
   consistencia entre ambos.
4. La eliminación de la validación que impedía solapar rangos con anulaciones previas
   (actualización 24-05-2022) implica que el sistema **no debe rechazar localmente** un
   ANECF por solapar rangos ya anulados — esa validación la hace DGII.
5. `<RncEmisor>` (con 'c' minúscula en 'Rn**c**') — diferente a `<RNCEmisor>` en otros
   formatos. El serializador XML debe respetar el casing exacto del tag.
6. El secuencial de e-NCF usa serie letra E-Z **excluyendo la letra P** — validación a
   implementar en el parser de e-NCF.

---

### 5.8 Firmado de e-CF — Proceso Técnico de Firma Digital (Marzo 2023)

**Documento:** "Firmado de e-CF.pdf" — 18 páginas. Publicado por DGII Gerencia de Facturación,
Marzo 2023. Contiene ejemplos funcionales en 5 lenguajes (C#/.NET, VB.Net, TypeScript/Node.js,
Java, PHP) y la estructura XML completa de la firma.

#### Estructura XML canónica del bloque `<Signature>`

```xml
<Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
  <SignedInfo>
    <CanonicalizationMethod
      Algorithm="http://www.w3.org/TR/2001/REC-xml-c14n-20010315" />
    <SignatureMethod
      Algorithm="http://www.w3.org/2001/04/xmldsig-more#rsa-sha256" />
    <Reference URI="">
      <Transforms>
        <Transform
          Algorithm="http://www.w3.org/2000/09/xmldsig#enveloped-signature" />
      </Transforms>
      <DigestMethod Algorithm="http://www.w3.org/2001/04/xmlenc#sha256" />
      <DigestValue>Atr9H7DiGlxrQOFII/hFihsL6ACiwe47Oo93tgtuera=</DigestValue>
    </Reference>
  </SignedInfo>
  <SignatureValue>gQyXO0FFDGIITESTpP5xZjLIRtv/Q7/ixe1lNDLDA5aw...</SignatureValue>
  <KeyInfo>
    <X509Data>
      <X509Certificate>DGIITESTBFagAwIBAgIInYGQUX9q0lwDQYJKoZIhvcNAQ...</X509Certificate>
    </X509Data>
  </KeyInfo>
</Signature>
```

**URIs exactos (copiar verbatim — no inventar variantes):**

| Elemento | Algorithm URI |
|---|---|
| CanonicalizationMethod | `http://www.w3.org/TR/2001/REC-xml-c14n-20010315` (C14N estándar, NO exclusive) |
| SignatureMethod | `http://www.w3.org/2001/04/xmldsig-more#rsa-sha256` |
| Transform | `http://www.w3.org/2000/09/xmldsig#enveloped-signature` |
| DigestMethod | `http://www.w3.org/2001/04/xmlenc#sha256` |

**Posición en el XML:** `<Signature>` se inserta como **último hijo del elemento raíz** `<ECF>`,
inmediatamente antes de `</ECF>`. Se hace con `DocumentElement.AppendChild(signatureNode)`.

**`Reference URI=""`** — firma sobre el documento completo (equivalente a URI="#" referenciando
el elemento raíz); la transformación enveloped-signature excluye el propio nodo `<Signature>`
del cómputo de digest.

**Certificado:** formato `.p12` (PKCS12), protegido con contraseña. El certificado se embebe
directamente en el XML como `<X509Certificate>` dentro de `<KeyInfo><X509Data>`.
**CRÍTICO:** el campo `SN` (Serial Number) del certificado debe coincidir con el RNC o
cédula del emisor — **validar ANTES de firmar**.

**`preserveWhiteSpace = false`** — obligatorio en TODOS los lenguajes/implementaciones.

#### Flujos de implementación por lenguaje

**C# (.NET):**
```csharp
// 1. Cargar certificado
X509Certificate2 cert = new X509Certificate2("cert.p12", "password");

// 2. Obtener RSA con CspParameters(24)  — PROV_RSA_AES, no el default
CspParameters csp = new CspParameters(24);
RSACryptoServiceProvider rsa = (RSACryptoServiceProvider)cert.PrivateKey;

// 3. Crear SignedXml sobre el documento cargado con preserveWhitespace=false
XmlDocument doc = new XmlDocument();
doc.PreserveWhitespace = false;
doc.Load("ecf.xml");
SignedXml signedXml = new SignedXml(doc);
signedXml.SigningKey = rsa;

// 4. Configurar Reference con Transform enveloped-signature
Reference reference = new Reference();
reference.Uri = "";
reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
signedXml.AddReference(reference);

// 5. Agregar KeyInfo con X509Certificate
KeyInfo keyInfo = new KeyInfo();
keyInfo.AddClause(new KeyInfoX509Data(cert));
signedXml.KeyInfo = keyInfo;

// 6. Firmar y adjuntar al documento
signedXml.ComputeSignature();
XmlElement xmlSignature = signedXml.GetXml();
doc.DocumentElement.AppendChild(xmlSignature);
```

**Java (oracle xmlparserv2 + javax.xml.crypto.dsig):**
```java
// Cargar keystore PKCS12
KeyStore ks = KeyStore.getInstance("PKCS12");
ks.load(new FileInputStream("cert.p12"), "password".toCharArray());

// CRÍTICO: parser.setPreserveWhitespace(false)
DOMParser parser = new DOMParser();
parser.setPreserveWhitespace(false);
parser.parse("ecf.xml");
Document doc = parser.getDocument();

// DOMSignContext con clave privada y nodo raíz
DOMSignContext dsc = new DOMSignContext(privateKey, doc.getDocumentElement());
XMLSignature signature = fac.newXMLSignature(signedInfo, keyInfo);
signature.sign(dsc);
```

**TypeScript/Node.js:**
- Librerías: `node-forge`, `xmldom`, `p12-pem`
- Canonicalización C14N manual antes del cómputo de digest
- Cargar .p12 con node-forge, convertir a PEM, usar con xmldsig

**PHP:**
- Librería: `selective-php/xmldsig`
- **Fix crítico:** `$element->C14N(true, false)` debe corregirse a `$element->C14N(false, false)`
  (primer argumento = `false` = C14N estándar, no exclusivo)
- Habilitar extensión openssl en php.ini

**VB.Net:** flujo idéntico a C#, mismo API de System.Security.Cryptography.Xml.

#### Hallazgos críticos del Firmado para el modelo de dominio

1. **CanonicalizationMethod estándar** (`REC-xml-c14n-20010315`), **NO exclusive C14N** —
   el algoritmo exclusivo (`#exc-c14n`) es incorrecto para DGII aunque es más común en otros
   sistemas; usar el URI exacto documentado.
2. **CspParameters(24)** en C# es obligatorio — corresponde a `PROV_RSA_AES`. Sin esto, el
   proveedor criptográfico default puede no soportar SHA-256.
3. **Validar `SN` del certificado = RNC/cédula ANTES de llamar a `ComputeSignature()`** —
   un certificado mal asociado genera una firma que DGII rechazará sin error claro.
4. **`preserveWhiteSpace = false`** en todos los lenguajes — confirmado; si el XML tiene
   indentación y se firma con `preserveWhiteSpace = true` la firma resultante es inválida.
5. **PHP fix `C14N(false, false)`** — la firma no se verifica si se usa el algoritmo exclusive
   en lugar del estándar; es un error silencioso difícil de detectar.
6. **El certificado se embebe en el XML** (`<X509Certificate>`) — DGII no acepta referencias
   externas al certificado; la clave pública debe ir en el documento firmado.
7. El `<CodigoSeguridadCF>` del QR = primeros 6 chars del **`<SignatureValue>`** (que es el
   hash de la firma, en Base64) — el campo se genera DESPUÉS de firmar, no antes.

---

### 5.9 Instructivo de Contingencia de FE — Gerencia de Facturación (Febrero 2026)

**Documento:** "Instructivo-Contingencia-FE.pdf" — 13 páginas. Publicado Febrero 2026.
Base legal: Ley Núm. 32-23, Norma 01-2020 (Art.3 literal i y Art.10), Decreto 587-24
(Reglamento, Arts. 40, 41, 42, 43).

#### Definiciones clave

- **Contingencia parcial:** falla que afecta solo algunas sucursales/unidades de negocio;
  el resto del contribuyente sigue operando normalmente.
- **Contingencia total:** falla que afecta la operación COMPLETA del contribuyente.

#### Tipo 1 — Falta de conectividad (sin notificación a DGII)

Cuando el emisor **no puede comunicarse con DGII** por falta de conectividad o intermitencia.

- El emisor genera e-CF **offline** normalmente (firma digital incluida).
- Remite los e-CF a DGII **en un plazo no mayor de 72 horas** desde que se restablezca
  la conexión. (Art. 40, Decreto 587-24)
- El RI entregado al cliente lleva la leyenda **EXACTA**:
  > *"e-CF emitido en modalidad de Contingencia, el cual podrá ser consultado para
  > su validez fiscal, a partir de las setenta y dos (72) horas."*
- **NO requiere notificación previa a DGII** — es un tipo de contingencia silencioso.
- El e-CF solo es fiscalmente válido **después** de que DGII lo procese (transcurridas
  las 72 horas o antes si se remite antes).

#### Tipo 2 — Imposibilidad técnica de emitir e-CF (con notificación obligatoria a DGII)

Cuando el emisor **no puede técnicamente** generar e-CF (falla del sistema de facturación).

- El contribuyente emite **comprobantes fiscales no electrónicos (Serie B)**.
- Duración máxima: **15 días calendario** (Art. 40, Decreto 587-24).
- **Debe notificarse a DGII** a través de la Oficina Virtual (OFV).

##### Flujo de Declaración Entrada en Contingencia (OFV)

1. OFV → Menú "Facturación Electrónica" → "Contingencia FE"
2. Seleccionar "Declaración Entrada en Contingencia"
3. Campos del formulario:
   - **Modalidad**: `Total` (toda la empresa) o `Parcial` (algunas sucursales)
   - **Fecha**: autocompletada por el sistema (fecha actual)
   - **Descripción**: texto libre describiendo la razón
4. Click "Guardar" → sistema responde: *"Registro insertado correctamente. Ahora se
   encuentra en estado de Contingencia."* → Click "Aceptar"
5. Se genera notificación en la Bandeja de Entrada de OFV:
   *"SE LE NOTIFICA QUE HA ENTRADO EN ESTADO DE CONTINGENCIA EN FECHA [fecha]"*

##### Flujo de Declaración Salida de Contingencia (OFV)

1. OFV → Menú "Facturación Electrónica" → "Contingencia FE"
2. Seleccionar "Declaración Salida de Contingencia"
3. Campos: **Fecha** (automática), **Descripción** (texto libre)
4. Click "Guardar" → sistema responde: *"Registro insertado correctamente. Ya no se
   encuentra en estado de Contingencia."*
5. Se genera notificación en Bandeja de Entrada:
   *"SE LE NOTIFICA QUE HA SALIDO DE ESTADO DE CONTINGENCIA EN FECHA [fecha]"*

##### Post-salida de contingencia tipo 2 — plazo de regularización

- Contribuyente tiene **30 días calendario** para enviar a DGII los e-CF que reemplazan
  los comprobantes no electrónicos emitidos durante la contingencia.
- Estos e-CF de reemplazo se envían **SOLO a DGII, no al receptor**.
- El receptor puede sustentar costos/gastos/crédito fiscal con el comprobante ordinario
  que recibió durante la contingencia.
- **Los comprobantes no electrónicos son válidos fiscalmente SOLO si la contingencia fue
  correctamente notificada a DGII** — sin notificación, no tienen validez fiscal.

##### Registro histórico de contingencias (OFV)

- OFV → Contingencia FE → parte inferior → "Histórico de Contingencias"
- Filtro: Todo / Total / Parcial
- Columnas: Id Evento, Fecha Entrada, Fecha Salida, Detalle (Ver/Ocultar)
- Al expandir un registro: Modalidad, Descripción de entrada, Descripción de Salida

#### Tipo 3 — Contingencia de la propia DGII

Cuando los **sistemas de DGII no están disponibles**:

- Los emisores almacenan los e-CF firmados localmente y los envían una vez restablecida
  la comunicación — no hay plazo explícito de 72h mencionado en este tipo.
- Si la contingencia de DGII supera **15 días hábiles**: la OFV habilita opción para
  enviar reportes alternativos de libros de ventas, compras, gastos, costos, retenciones,
  operando con comprobantes no electrónicos.

#### Resumen de plazos de contingencia (tabla consolidada)

| Tipo | Situación | Plazo máximo contingencia | Plazo regularización | Notificación DGII |
|---|---|---|---|---|
| 1 | Falta de conectividad | Sin límite explícito | 72 h para remitir e-CF a DGII | NO requerida |
| 2 | Imposibilidad técnica | 15 días calendario | 30 días para reemplazar con e-CF | SÍ — OFV obligatorio |
| 3 | Falla de DGII | Hasta >15 días hábiles | Al reestablecerse | NO (es falla de DGII) |

#### Hallazgos críticos de contingencia para el modelo de dominio

1. **Tipo 1 (falta de conectividad) NO requiere notificación a DGII** — el sistema
   puede activarlo automáticamente al detectar timeout en la comunicación con DGII.
   Solo requiere almacenar el e-CF y marcarlo para reenvío.
2. **Tipo 2 (imposibilidad técnica) SÍ requiere notificación manual vía OFV** — el
   sistema debe proveer UI/dashboard para que el cliente declare la contingencia, con
   campos: Modalidad (Total/Parcial) y Descripción. La fecha la pone DGII.
3. **Leyenda obligatoria exacta en el RI tipo 1** — el texto es prescrito por DGII y
   debe reproducirse verbatim; no parafrasear.
4. **Los e-CF de reemplazo (tipo 2, post-salida) van SOLO a DGII** — el endpoint de
   envío al receptor NO debe usarse para estos e-CF.
5. **Comprobantes ordinarios (Serie B) en contingencia tipo 2**: solo son fiscalmente
   válidos si la contingencia fue notificada; el dashboard debe mostrar claramente si
   el cliente notificó o no, para que sus receptores puedan determinar validez.
6. **Historial de contingencias accesible en OFV** — la API de DGII debería exponer
   este historial; el sistema puede consultarlo para auditoría o para detectar si el
   cliente está actualmente en contingencia.
7. **Contingencia parcial vs total** — el modelo debe registrar la modalidad declarada
   (campo en la entidad de contingencia). Si es parcial, algunas sucursales siguen
   en modo normal; la lógica de ruteo debe considerar esto.
8. **Contacto DGII actualizado (Febrero 2026):** Centro de Contacto: (809) 689-3444,
   opción 4. Correo: facturacionelectronica@dgii.gov.do. (El número (809) 287-2009 del
   documento de Firmado 2023 puede estar desactualizado.)

---

### 5.10 Descripción Técnica de Servicios DGII (v1.7, Mayo 2026) — ENDPOINTS VERIFICADOS

**Documento:** "Descripcion Tecnica Servicios DGII.pdf" — 46 páginas. Versión 1.7, Mayo 2026.
GERENCIA DE TECNOLOGÍA DE LA INFORMACIÓN Y COMUNICACIONES, DGII.
**Nota:** Este documento fue separado del documento original en la actualización 02-01-2026;
anteriormente era uno solo con "Descripción Técnica Servicios Emisores Electrónicos".

**⚠ CORRECCIÓN ERROR #1 DEL PLAN TÉCNICO**: Los endpoints verificados a continuación
reemplazan completamente los del plan técnico previo.

#### Dominios base

| Servicio | Dominio |
|---|---|
| e-CF normal (todos excepto RFCE) | `ecf.dgii.gov.do` |
| RFCE (Tipo 32 < RD$250,000) | `fc.dgii.gov.do` |
| Status de servicios | `statusecf.dgii.gov.do` (API key propia) |

#### Ambientes

| Ambiente | Propósito | Segmento URL |
|---|---|---|
| TesteCF | Pre-certificación (pruebas libres) | `/testecf/` |
| CerteCF | Certificación (homologación oficial) | `/certecf/` |
| eCF | Producción | `/ecf/` |

---

#### A. Autenticación — `ecf.dgii.gov.do/{ambiente}/autenticacion`

| Paso | Método | Endpoint | Input | Output |
|---|---|---|---|---|
| 1. Semilla | GET | `/api/autenticacion/semilla` | — | XML semilla (firmar) |
| 2. Token | POST | `/api/autenticacion/validarsemilla` | `xml*` (multipart/form-data) | JSON token |

**Respuesta token:**
```json
{
  "token": "string",
  "expira": "yyyy-MM-ddTHH:mm:ssZ",
  "expedido": "yyyy-MM-ddTHH:mm:ssZ"
}
```
**Header auth en todos los servicios:** `Authorization: Bearer {token}`
**Expiración:** 1 hora desde `expedido`. Renovar proactivamente.

---

#### B. Recepción de e-CF — `ecf.dgii.gov.do/{ambiente}/recepcion`

**Disponible en:** TesteCF, CerteCF, eCF

| Método | Endpoint | Input | Output |
|---|---|---|---|
| POST | `/api/facturaselectronicas` | `xml*` (multipart/form-data) | JSON trackId |

**Respuesta:**
```json
{ "trackId": "string", "error": "string", "mensaje": "string" }
```
**⚠ REGLA CRÍTICA:** Tipo 32 con monto < RD$250,000 → NO usar este endpoint → usar Recepción RFCE (dominio diferente).

---

#### C. Recepción RFCE — `fc.dgii.gov.do/{ambiente}/recepcionfc/`

**Dominio distinto (`fc.dgii.gov.do`, no `ecf.dgii.gov.do`)**

| Ambiente | URL completa |
|---|---|
| TesteCF | `https://fc.dgii.gov.do/testecf/recepcionfc/` |
| CerteCF | `https://fc.dgii.gov.do/Certecf/recepcionfc/` |
| eCF | `https://fc.dgii.gov.do/ecf/recepcionfc/` |

| Método | Endpoint | Input | Output |
|---|---|---|---|
| POST | `/api/recepcion/ecf` | `xml*` (multipart/form-data) | JSON resultado |

**Respuesta:**
```json
{
  "codigo": 1,
  "estado": "string",
  "mensajes": [{ "codigo": "string", "valor": "string" }],
  "encf": "string",
  "secuenciaUtilizada": true
}
```
**`secuenciaUtilizada`:** `true` = NO reutilizable; `false` = SÍ reutilizable.
Casos NO reutilizables: firma inválida, XML inválido, firmante no autorizado, e-NCF no autorizado/vencido, RNC no emisor/no existe/no activo.

---

#### D. Consulta RFCE — `fc.dgii.gov.do/{ambiente}/consultarfce`

| Método | Endpoint | Parámetros | Output |
|---|---|---|---|
| GET | `/api/Consultas/Consulta` | `RNC_Emisor=*`, `ENCF=*`, `Cod_Seguridad_eCF=*` | JSON |

**Respuesta:**
```json
{
  "rnc": "string",
  "encf": "string",
  "secuenciaUtilizada": true,
  "codigo": "string",
  "estado": "string",
  "mensajes": [{ "valor": "string", "codigo": 0 }]
}
```
**Estados de salida:** 0=No encontrado, 1=Aceptado (validez fiscal), 2=Rechazado (nulidad).
*Nota temporal: también retorna "Aceptado condicional" para tipo 32 < RD$250,000 durante período de transición.*

---

#### E. Consulta resultado e-CF — `ecf.dgii.gov.do/{ambiente}/consultaresultado`

**Disponible en:** TesteCF, CerteCF, eCF

| Método | Endpoint | Input | Output |
|---|---|---|---|
| GET | `/api/consultas/estado` | `trackid=*` (query param) | JSON |

**Request URL completa:**
```
https://ecf.dgii.gov.do/{ambiente}/consultaresultado/api/consultas/estado?trackid={trackid}
```

**Respuesta:**
```json
{
  "trackId": "string",
  "codigo": 0,
  "estado": "string",
  "rnc": "string",
  "eNCF": "string",
  "secuenciaUtilizada": true,
  "fechaRecepcion": "string",
  "mensajes": [{ "valor": "string", "codigo": 0 }]
}
```

**Estados de salida:**
| Código | Estado | Significado |
|---|---|---|
| 0 | No encontrado | trackId no en registros (puede estar en proceso aún) |
| 1 | Aceptado | e-CF válido, tiene validez fiscal |
| 2 | Rechazado | Nulidad del comprobante |
| 3 | En Proceso | Aún no validado — esperar y reintentar. **Promedio: 200ms** |
| 4 | Aceptado Condicional | No cumplió algún punto pero no ameritó rechazo; tiene validez fiscal |

**`secuenciaUtilizada`:** misma semántica que en Recepción RFCE.

---

#### F. Consulta estado e-CF — `ecf.dgii.gov.do/{ambiente}/consultaestado`

**⚠ Disponible solo en:** TesteCF y eCF (producción) — **NO listado para CerteCF**

Permite consultar estado de un e-CF conociendo RNC+e-NCF (sin necesitar trackId). También consulta e-CF remitidos vía RFCE < RD$250,000. Requiere que el autenticado esté **delegado** para el emisor o receptor.

| Método | Endpoint | Input | Output |
|---|---|---|---|
| GET | `/api/consultas/estado` | `rncemisor=*`, `ncfelectronico=*`, `rnccomprador`, `codigoseguridad` | JSON |

**Request URL completa:**
```
https://ecf.dgii.gov.do/{ambiente}/consultaestado/api/consultas/estado?rncemisor={rncemisor}&ncfelectronico={ncfelectronico}&rnccomprador={rnccomprador}&codigoseguridad={codigoseguridad}
```

**Respuesta JSON:**
```json
{
  "codigo": 0,
  "estado": "string",
  "rncEmisor": "string",
  "ncfElectronico": "string",
  "montoTotal": 0,
  "totalITBIS": 0,
  "fechaEmision": "string",
  "fechaFirma": "string",
  "rncComprador": "string",
  "codigoSeguridad": "string",
  "idExtranjero": "string"
}
```
**Estados:** 0=No encontrado, 1=Aceptado, 2=Rechazado (+ Aceptado Condicional para tipo 32 < 250k).

---

#### G. Consulta trackId e-CF — `ecf.dgii.gov.do/{ambiente}/consultatrackids`

**⚠ Disponible solo en:** TesteCF y eCF — **NO listado para CerteCF**

Retorna **lista** de trackIds de un e-NCF específico (puede haber múltiples si se remitió varias veces el mismo e-NCF). Requiere estar **delegado** para el emisor.

| Método | Endpoint | Input | Output |
|---|---|---|---|
| GET | `/api/trackids/consulta` | `rncemisor=*`, `encf=*` | JSON / XML |

**Respuesta JSON:**
```json
{ "trackId": "string", "estado": "string", "fechaRecepcion": "string" }
```
**Estados:** No encontrado, Aceptado, Rechazado, Aceptado Condicional, En proceso (reintentar, promedio 200ms).

---

#### H. Recepción de Aprobación Comercial — `ecf.dgii.gov.do/{ambiente}/aprobacioncomercial`

**Disponible en:** TesteCF, CerteCF, eCF

Recibe XML de Aprobación Comercial (ACECF) enviado por el receptor. Receptor envía simultáneamente al emisor y a DGII.

| Método | Endpoint | Input | Output |
|---|---|---|---|
| POST | `/api/aprobacioncomercial` | `xml*` (multipart/form-data) | JSON |

**Respuesta:**
```json
{ "mensaje": ["string"], "estado": "string", "codigo": "string" }
```
**Estados:** 1=Aprobación comercial aprobada, 2=Aprobación comercial rechazada.

---

#### I. Anulación de e-NCF — `ecf.dgii.gov.do/{ambiente}/anulacionrangos`

**⚠ Disponible solo en:** TesteCF y eCF — **NO listado para CerteCF**

Recibe XML ANECF para anular rangos de secuencias no utilizadas.

| Método | Endpoint | Input | Output |
|---|---|---|---|
| POST | `/api/operaciones/anularrango` | `xml*` (multipart/form-data) | JSON |

**Respuesta:**
```json
{ "rnc": "string", "codigo": "string", "nombre": "string", "mensajes": ["string"] }
```

---

#### J. Consulta Directorio de Servicios — `ecf.dgii.gov.do/{ambiente}/consultadirectorio`

**⚠ Disponible solo en:** TesteCF y eCF — **NO listado para CerteCF**

En TesteCF: retorna URLs del servicio de emisor-receptor de DGII (para simular B2B).
En producción: retorna los URLs de servicio de todos los contribuyentes electrónicos.

**Sub-endpoint 1 — Listado completo:**

| Método | Endpoint | Input | Output |
|---|---|---|---|
| GET | `/api/consultas/listado` | — | JSON array |

**Respuesta:**
```json
[{
  "nombre": "string",
  "rnc": "string",
  "urlRecepcion": "string",
  "urlAceptacion": "string",
  "urlOpcional": "string"
}]
```
- `urlRecepcion` = host del servicio de recepción de e-CF del contribuyente
- `urlAceptacion` = host del servicio de aprobación comercial del contribuyente
- `urlOpcional` = host del servicio de autenticación del contribuyente (si lo usa)

**Sub-endpoint 2 — Por RNC:**

| Método | Endpoint | Input | Output |
|---|---|---|---|
| GET | `/api/consultas/obtenerDirectorioporrnc` | `RNC=*` | JSON |

Misma estructura de respuesta pero un solo objeto.

---

#### K. Consulta Timbre (QR) e-CF — `ecf.dgii.gov.do/{ambiente}/consultatimbre`

**Disponible en:** TesteCF, CerteCF, eCF

Valida e-CF enviado vía Recepción e-CF a partir de datos del timbre QR de la RI.
**No tiene endpoint REST tradicional** — es una URL construida desde parámetros concatenados.

**Parámetros para construir la URL del QR:**
RncEmisor · RncComprador · ENCF · FechaEmision · MontoTotal · FechaFirma · CodigoSeguridad

**Ejemplo de URL construida (la que va en el código QR):**
```
https://ecf.dgii.gov.do/testecf/consultatimbre?rncemisor=130000001&rnccomprador=130000002
&encf=e310000000001&fechaemision=10-10-2020&montototal=02.11
&fechafirma=10-10-2020%2009:00:00&codigoseguridad=dcp79q
```

**Versión QR:** Versión 8 (https://www.qrcode.com/en/about/version.html)

**Salida:** RNC Emisor, Razón social emisor, RNC Comprador, Razón social comprador, e-NCF, Fecha Emisión, Total ITBIS, Monto Total, Estado.
**Estados:** No fue encontrada la factura · Aceptado (incluye Aceptado Condicional) · Rechazado.

---

#### L. Consulta Timbre FC (QR) RFCE — `fc.dgii.gov.do/{ambiente}/consultatimbrefc`

**Disponible en:** TesteCF, CerteCF, eCF (dominio `fc.dgii.gov.do`)

Valida RFCE (Tipo 32 < RD$250,000) enviado vía Recepción RFCE.

**Parámetros:** RNCEmisor · e-NCF · MontoTotal · CódigoSeguridad

**Ejemplo URL QR para RFCE:**
```
https://fc.dgii.gov.do/testecf/consultatimbrefc?rncemisor=131880738&encf=e320000000064
&montototal=6225.09&codigoseguridad=uabnyh
```

**Salida:** RNC Emisor, Razón Social, e-NCF, Estado.
**Estados:** No fue encontrada · Aceptado (incluye Aceptado Condicional) · Rechazado.

---

#### M. Servicio Comunicación Emisor-Receptor (TesteCF únicamente)

**⚠ Disponible SOLO en TesteCF** — Simulador B2B gestionado por DGII para pruebas.
URL base: `https://ecf.dgii.gov.do/testecf/emisorreceptor`

Este servicio simula ser un contribuyente receptor. Permite al desarrollador probar el flujo B2B completo sin tener otro contribuyente real.

Expone los mismos endpoints que cada contribuyente debe implementar en su servidor:

| Endpoint | Método | Descripción |
|---|---|---|
| `/fe/autenticacion/api/semilla` | GET | Retorna XML semilla para firmar |
| `/fe/autenticacion/api/validacioncertificado` | POST | Input: `xml*` → retorna token JWT |

**⚠ DISTINCIÓN CRÍTICA B2B vs DGII:**
- Endpoints de DGII (para enviar e-CFs): `/api/autenticacion/semilla` y `/api/autenticacion/validarsemilla`
- Endpoints B2B (que cada contribuyente expone y que el emisor-receptor de DGII replica):
  `/fe/autenticacion/api/semilla` y `/fe/autenticacion/api/validacioncertificado`

---

#### N. Tabla resumen completa de todos los endpoints

| Servicio | Dominio | Base path | Método | Endpoint | Disponibilidad |
|---|---|---|---|---|---|
| Semilla auth | ecf | /{amb}/autenticacion | GET | /api/autenticacion/semilla | T,C,P |
| Validar semilla | ecf | /{amb}/autenticacion | POST | /api/autenticacion/validarsemilla | T,C,P |
| Recepción e-CF | ecf | /{amb}/recepcion | POST | /api/facturaselectronicas | T,C,P |
| Recepción RFCE | fc | /{amb}/recepcionfc | POST | /api/recepcion/ecf | T,C,P |
| Consulta RFCE | fc | /{amb}/consultarfce | GET | /api/Consultas/Consulta | T,C,P |
| Consulta resultado e-CF | ecf | /{amb}/consultaresultado | GET | /api/consultas/estado | T,C,P |
| Consulta estado e-CF | ecf | /{amb}/consultaestado | GET | /api/consultas/estado | T,P |
| Consulta trackId e-CF | ecf | /{amb}/consultatrackids | GET | /api/trackids/consulta | T,P |
| Aprobación comercial | ecf | /{amb}/aprobacioncomercial | POST | /api/aprobacioncomercial | T,C,P |
| Anulación e-NCF | ecf | /{amb}/anulacionrangos | POST | /api/operaciones/anularrango | T,P |
| Directorio listado | ecf | /{amb}/consultadirectorio | GET | /api/consultas/listado | T,P |
| Directorio por RNC | ecf | /{amb}/consultadirectorio | GET | /api/consultas/obtenerDirectorioporrnc | T,P |
| QR e-CF | ecf | /{amb}/consultatimbre | GET | (URL directa con params) | T,C,P |
| QR RFCE | fc | /{amb}/consultatimbrefc | GET | (URL directa con params) | T,C,P |
| B2B Auth semilla | ecf | /testecf/emisorreceptor | GET | /fe/autenticacion/api/semilla | Solo T |
| B2B Auth token | ecf | /testecf/emisorreceptor | POST | /fe/autenticacion/api/validacioncertificado | Solo T |

*T=TesteCF, C=CerteCF, P=Producción (eCF)*

#### Recomendaciones DGII (verbatim del documento)

1. Verificar URLs correctas antes de enviar (para evitar inconvenientes de recepción).
2. Tipo 32: existen dos servicios de recepción según el monto (Recepción e-CF vs RFCE).
3. Para RI de tipo 32: existen dos consultas QR (consultatimbre vs consultatimbrefc).
4. Consulta estado e-CF: confirmar estar delegado para el emisor o receptor.
5. Familiarizarse con las validaciones de los formatos (XML) para todas las operaciones.

#### Hallazgos críticos del documento de servicios para el modelo de dominio

1. **Error #1 CONFIRMADO Y RESUELTO:** El endpoint real de recepción es `POST /api/facturaselectronicas` bajo `ecf.dgii.gov.do/{ambiente}/recepcion`, NO `/fe/recepcion/api/ecf` (esa es la ruta B2B que cada contribuyente expone).
2. **Endpoint `/api/consultas/estado` reutilizado con base path diferente** — el mismo endpoint sub-path sirve para dos servicios distintos: bajo `consultaresultado` (requiere trackId) y bajo `consultaestado` (requiere RNC+eNCF). El cliente HTTP debe construir la URL completa incluyendo el base path del servicio.
3. **CerteCF ausente en varios servicios** — `consultaestado`, `consultatrackids`, `anulacionrangos`, y `consultadirectorio` no listan CerteCF como ambiente disponible. Sólo tienen TesteCF y eCF. Esto es crítico para las pruebas de certificación.
4. **B2B endpoints son distintos a los de DGII** — Los endpoints que cada contribuyente debe exponer para recibir e-CFs de otros (y que el emisor-receptor de DGII replica) son `/fe/autenticacion/api/semilla` y `/fe/autenticacion/api/validacioncertificado`. No confundir con los endpoints de DGII.
5. **QR URL es la URL de consultatimbre completa** — el QR no es solo el código de seguridad; es la URL completa construida con todos los parámetros. El generador de RI debe construir esta URL correctamente con URL-encoding de fecha y hora.
6. **Dos variantes de QR según el tipo de e-CF** — e-CF normal → consultatimbre (ecf.dgii.gov.do) con 7 parámetros; RFCE → consultatimbrefc (fc.dgii.gov.do) con solo 4 parámetros (sin RncComprador, FechaEmision, FechaFirma).
7. **Directorio de servicios** — la respuesta incluye 3 URLs por contribuyente: recepción, aprobación comercial, y autenticación opcional. El emisor las usa para enviar B2B directamente, no siempre a través de DGII.

---

### 5.11 Descripción Técnica de Servicios Emisores Electrónicos (v1.7, Mayo 2026) — SPECS B2B VERIFICADAS

**Documento:** "Descripcion Tecnica Emisores Electronicos.pdf" — 13 páginas  
**Emisor:** GERENCIA DE TECNOLOGÍA DE LA INFORMACIÓN Y COMUNICACIONES — DGII  
**Nota:** Documento separado del doc de Servicios DGII en enero 2026.

---

#### A. Nombres de Archivo XML por Formato (CONFIRMADOS)

| Formato | Nombre de archivo | Ejemplo |
|---|---|---|
| e-CF | RNCEmisor + e-NCF | `101672919E310000000001.xml` |
| Aprobación Comercial (ACECF) | RNCComprador + e-NCF | `101672919E310000000001.xml` |
| Acuse de Recibo (ARECF) | RNCComprador + e-NCF | `101672919E310000000001.xml` |
| RFCE (tipo 32 <250k) | RNCEmisor + e-NCF | `101672919E320000000001.xml` |

> **Crítico:** Para ACECF y ARECF, el nombre usa el RNC del **comprador**, no del emisor.

---

#### B. Tabla de Escape de Caracteres XML (campos ALFANUM) — actualizada 03-04-2025

| Carácter | Escape | Decimal | Hex |
|---|---|---|---|
| `"` | `&quot;` | `&#34;` | `&#x22;` |
| `'` | `&apos;` | `&#39;` | `&#x27;` |
| `<` | `&lt;` | `&#60;` | `&#x3C;` |
| `>` | `&gt;` | `&#62;` | `&#x3E;` |
| `&` | `&amp;` | `&#38;` | `&#x26;` |
| `©` | `&copy;` | `&#169;` | `&#xA9;` |
| `€` | `&euro;` | `&#8364;` | `&#x20AC;` |
| `®` | `&reg;` | `&#174;` | `&#xAE;` |

> **Nota:** Los 3 primeros caracteres (`"`, `'`, `<`) son los más frecuentes. El serializador XML debe aplicar este escape en todos los campos ALFANUM antes de firmar.

---

#### C. URL Encoding para QR (código de seguridad en RI)

Caracteres que deben ser percent-encoded al construir la URL del QR:

```
Espacio → %20    ! → %21    # → %23    $ → %24    & → %26
' → %27          ( → %28    ) → %29    * → %2A    + → %2B
, → %2C          / → %2F    : → %3A    ; → %3B    = → %3D
? → %3F          @ → %40    [ → %5B    ] → %5D    " → %22
- → %2D          . → %2E    < → %3C    > → %3E    \ → %5C
^ → %5E          _ → %5F    ` → %60
```

> **Implicación:** La fecha y hora en la URL del QR deben ir URL-encoded. Ej: `2022-07-27T11:59:31` → `2022-07-27T11%3A59%3A31` (los `:` se encodean como `%3A`).

---

#### D. Firmado de XML — Reglas CONFIRMADAS

- **Algoritmo:** SHA-256 (XMLDSig)
- **SN del certificado:** debe ser el RNC, Cédula o Pasaporte del propietario
- **preserveWhiteSpace:** `false` (C14N estándar, NOT exclusive)
- **Inmutabilidad:** El XML firmado **no puede ser alterado en ninguna circunstancia**

---

#### E. Reglas Generales de los Servicios B2B (que cada contribuyente debe exponer)

1. Usar **SSL** (HTTPS)
2. Emplear **puertos de red tradicionales** (80, 443)
3. **NO ser sensibles** a mayúsculas/minúsculas en paths
4. Ser **accesibles desde internet** (no solo desde intranet)
5. **NO estar en listas negras** ni categorizados como proxy avoidance

> **Implicación arquitectural:** Los endpoints B2B que exponga nuestro SaaS deben cumplir estos 5 requisitos. El certificado SSL debe ser válido (no autofirmado). Los puertos deben ser estándar.

---

#### F. Endpoint B2B #1: URL de Autenticación (OPCIONAL)

Cada contribuyente PUEDE exponer autenticación en su servidor B2B. Si lo hace, el emisor que le envíe e-CFs debe obtener un token antes de enviar.

**GET `/fe/autenticacion/api/semilla`**

| Campo | Valor |
|---|---|
| Input | N/A |
| Output | XML `semillamodel` |

```xml
<?xml version="1.0" encoding="utf-8"?>
<semillamodel xmlns:xsi="..." xmlns:xsd="...">
  <valor>0xggeol2rfxxmt22g4abxa91ycxblmyeci6h1+519rudfciuqf2ytd7wdftm1m1z39g4...</valor>
  <fecha>2022-07-27T11:59:31.3551245-04:00</fecha>
</semillamodel>
```

**POST `/fe/autenticacion/api/validacioncertificado`**

| Campo | Valor |
|---|---|
| Input | `xml` (semilla firmada) — `multipart/form-data` |
| Output JSON | `{"token":"string","expira":"yyyy-MM-ddTHH:mm:ssZ","expedido":"yyyy-MM-ddTHH:mm:ssZ"}` |
| Output XML | `<respuestaautenticacion><token>...</token><expira>...</expira><expedido>...</expedido></respuestaautenticacion>` |
| Referencia | RFC 6750 (Bearer Token) |

---

#### G. Endpoint B2B #2: URL de Recepción (OBLIGATORIA)

**POST `/fe/recepcion/api/ecf`**

| Campo | Valor |
|---|---|
| Input | `xml` (Formato e-CF) |
| Authorization | `Bearer {token}` — condicional: solo si el receptor declaró autenticación |
| Output | **XML ARECF firmado digitalmente** |

> **CRÍTICO:** La respuesta no es solo HTTP 200 — es un **ARECF firmado** que el emisor debe procesar y almacenar. Error arquitectural si se ignora.

Ejemplo de respuesta ARECF:

```xml
<?xml version="1.0" encoding="utf-8"?>
<arecf xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
       xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <detalleacusedeRecibo>
    <version>1.0</version>
    <rncemisor>131880600</rncemisor>
    <rnccomprador>132880600</rnccomprador>
    <encf>E310000000001</encf>
    <estado>0</estado>
    <fechahoraacuserecibo>17-12-2020 11:19:06</fechahoraacuserecibo>
  </detalleacusedeRecibo>
</arecf>
```

---

#### H. Endpoint B2B #3: URL de Aprobación Comercial (OBLIGATORIA)

**POST `/fe/aprobacioncomercial/api/ecf`**

| Campo | Valor |
|---|---|
| Input | `xml` (Formato ACECF) |
| Authorization | `Bearer {token}` — condicional |
| Output éxito | HTTP 200 |
| Output fallo | HTTP 400 |

---

#### I. Patrón de URL Completo para B2B

```
https://[host]/[ambiente]/[nombreservicio]/fe/recepcion/api/ecf
```

Donde `[ambiente]` es el ambiente del emisor (TesteCF, CerteCF, eCF) y `[nombreservicio]` es el identificador del servicio registrado en DGII.

---

#### J. Hallazgos críticos del documento Emisores Electrónicos

1. **Autenticación B2B es OPCIONAL, Recepción y Aprobación Comercial son OBLIGATORIAS.** Nuestro SaaS debe exponer al menos los endpoints `/fe/recepcion/api/ecf` y `/fe/aprobacioncomercial/api/ecf`. La autenticación B2B es opcional pero recomendable implementarla para poder recibir de emisores que la requieren.

2. **La recepción B2B retorna un ARECF firmado** (no solo HTTP 200). Esto significa que cuando nuestro SaaS recibe un e-CF de otro emisor vía B2B, debe: (a) validar el e-CF recibido, (b) generar y firmar un ARECF de respuesta, (c) retornarlo en el cuerpo de la respuesta HTTP. El flujo es bidireccional y firmado.

3. **Nombres de archivo críticos para ACECF y ARECF:** Usan el RNC del **comprador** como prefijo, no el del emisor. El módulo de generación de archivos debe parametrizar esto por formato.

4. **La tabla de escape XML fue actualizada en abril 2025** — versiones anteriores del doc no incluían `©`, `€`, `®`. Si se tiene una implementación basada en el doc anterior, revisar el serializador.

5. **URL encoding en QR es extenso** — más de 30 caracteres requieren encoding. En particular, la fecha/hora lleva `:` que se encodean como `%3A`. El generador de RI debe usar una función de URL encoding robusta, no construir la URL manualmente.

6. **Reglas B2B para DGII TesteCF:** En el ambiente de pruebas (`testecf.emisorreceptor`), DGII actúa como receptor simulado. El emisor que prueba envía a DGII quien actúa de proxy, verificando que el contribuyente destino exponga los endpoints B2B correctamente.

---

### 5.12 Proceso de Certificación para Emisores Electrónicos (Mayo 2026) — DOS TRACKS

**Fuentes:**
- Doc A: "Proceso-Certificacion-EmisorElectronico-Proveedor-Servicios-FECertificado.pdf" (17 págs, 968KB) — Track con Proveedor Certificado
- Doc B: "Proceso de Certificacion para ser Emisor Electronico.pdf" (20 págs, 1.6MB) — Track General (software propio o externo)

---

#### A. Requisitos Previos (comunes a ambos tracks)

1. Estar inscrito en el **RNC** (Registro Nacional de Contribuyentes)
2. Poseer clave de acceso a la **OFV** (Oficina Virtual), tanto el contribuyente como el representante
3. Tener autorización para emitir comprobantes fiscales (**Alta NCF**)
4. Disponer de **certificado digital** para Procedimiento Tributario emitido por entidad certificadora autorizada por **INDOTEL**, que corresponda a la persona que actuará como representante/Usuario Administrador e-CF
5. Disponer de un **software** para la emisión de e-CF
6. Estar al día en el cumplimiento de sus **obligaciones tributarias**

> **Nota sobre el certificado:** El representante debe estar vinculado al RNC del contribuyente solicitante. El campo `SN` del certificado digital debe corresponder al RNC, cédula o pasaporte del representante.

---

#### B. Dos Tracks de Certificación

| Característica | Track A: Con Proveedor Certificado | Track B: Software Propio |
|---|---|---|
| Tipo de Software | EXTERNO (proveedor certificado) | PROPIO o EXTERNO |
| Pruebas de Datos e-CF | ❌ No aplica | ✅ 21 comprobantes + 4 Resúmenes |
| Pruebas Aprobación Comercial | ❌ No aplica | ✅ 11 aprobaciones |
| Pruebas de Simulación | ✅ 1 por tipo (mínimo) | ✅ 4/31, 2/32, 1/33, 2/34, 2/41, etc. |
| Envío RI (Representación Impresa) | ❌ No aplica | ✅ PDF por cada tipo, max 10MB |
| Validación RI por DGII | ❌ No aplica | ✅ Aprobada/Rechazada |
| Pruebas de Comunicación (recepción) | ❌ No aplica | ✅ DGII envía e-CFs al sistema del emisor |
| Recepción de Aprobaciones Comerciales | ❌ No aplica | ✅ DGII envía ACECF al sistema |
| Formulario de postulación | ✅ Datos del proveedor OBLIGATORIOS | ✅ Datos del proveedor si EXTERNO |

> **Impacto estratégico crítico:** Cuando nuestros clientes nos usen como proveedor certificado (Track A), se saltan las Pruebas de Datos, las pruebas de RI y las pruebas de comunicación. Solo hacen simulaciones (1 por tipo). Esto **reduce significativamente la carga** de certificación de nuestros clientes — ventaja competitiva real.

---

#### C. Flujo de Pasos — Track B (General, 15 pasos)

| Paso | Nombre | Descripción |
|---|---|---|
| 1 | Registrado / Formulario | Postulación: datos contribuyente, representante, software, URLs B2B |
| 2 | Pruebas de Datos e-CF | Descarga set de prueba Excel de DGII → genera 21 e-CF XML → envía a CerteCF |
| 3 | Prueba de Datos Aprobaciones | Descarga set Excel → genera 11 ACECF → envía a `CerteCF/AprobacionComercial` |
| 4 | Pruebas de Simulación e-CF | Genera e-CF con datos reales propios → envía (ver cantidades por tipo abajo) |
| 5 | Simulación Representación Impresa | Envía PDF del RI de cada e-CF de simulación (max 10MB total) |
| 6 | Validación Representación Impresa | DGII valida RI → Aprobada o Rechazada (reenviar si rechazada) |
| 7 | URL Servicio de Prueba | Confirmar/actualizar URLs de recepción, aprobación, autenticación |
| 8 | Inicio Prueba Recepción e-CF | DGII envía e-CFs al sistema del emisor; emisor debe retornar ARECF firmado |
| 9 | Recepción de e-CF | DGII valida la capacidad de recepción del sistema |
| 10 | Inicio Prueba Aprobaciones | DGII envía ACECF al sistema del emisor; emisor recibe y responde |
| 11 | Recepción Aprobaciones Comerciales | Emisor responde "OK" o "Error/Incorrecto" |
| 12 | URL Servicios Producción | Confirmar URLs definitivas de producción |
| 13 | Declaración Jurada | XML firmado bajo juramento; DGII valida representante |
| 14 | Verificación Estatus | DGII re-verifica RNC, OFV, Alta NCF, representante activo |
| 15 | Finalizado | Autorización otorgada; OFV habilitada con menú FE completo |

---

#### D. Cantidades en Pruebas de Simulación (Paso 4 — Track B)

| Tipo | Cantidad | Tipo | Cantidad |
|---|---|---|---|
| 31 (Factura Crédito Fiscal) | 4 | 32 ≥250k | 2 |
| 33 (Nota de Débito) | 1 | 34 (Nota de Crédito) | 2 |
| 41 (Compras) | 2 | 43 (Gastos Menores) | 2 |
| 44 (Regímenes Especiales) | 2 | 45 (Gubernamental) | 2 |
| 46 (Exportaciones) | 2 | 47 (Pagos Exterior) | 2 |
| 32 RFCE (resumen) | 4 | 32 <250k (completa) | 4 |

**Orden obligatorio de emisión:**
1. **Primero:** 31, 32≥250k, 41, 43, 44, 45, 46, 47
2. **Segundo:** 33 (ND), 34 (NC)
3. **Tercero:** 32 RFCE (resumen de consumo)
4. **Cuarto:** 32 <250k (Factura completa — solo después de que el RFCE sea aceptado)

> **Regla crítica:** Para tipo 32 <250k: primero enviar el RFCE a `https://fc.dgii.gov.do/CerteCF/RecepcionFC`. Una vez aceptado, cargar el XML completo de la factura en la interfaz "Facturas de consumo < 250Mil" del portal.

---

#### E. URLs DGII Confirmadas para CerteCF (VERBATIM de ambos documentos)

| Servicio | URL Confirmada |
|---|---|
| Autenticación | `https://eCF.dgii.gov.do/CerteCF/Autenticacion` |
| Recepción e-CF | `https://eCF.dgii.gov.do/CerteCF/Recepcion` |
| Consulta Resultado | `https://eCF.dgii.gov.do/CerteCF/ConsultaResultado` |
| Recepción RFCE | `https://fc.dgii.gov.do/CerteCF/RecepcionFC` |
| Aprobación Comercial | `https://eCF.dgii.gov.do/CerteCF/AprobacionComercial` |

> **Nota:** Las mismas URLs para TesteCF reemplazando `CerteCF` → `TesteCF`.

---

#### F. Formulario de Postulación — Campos que el cliente llena con NUESTRAS URLs

En el formulario de postulación (Paso 1), el cliente que nos usa como proveedor declara:

| Campo | Path fijo que DGII pre-rellena | Lo que el cliente ingresa |
|---|---|---|
| URL de Recepción | `/fe/recepcion/api/ecf` | `https://[nuestro-host]/[ambiente]/...` |
| URL de Aprobación | `/fe/aprobacioncomercial/api/ecf` | `https://[nuestro-host]/[ambiente]/...` |
| URL de Autenticación | `/fe/autenticacion/api/[semilla\|validacioncertificado]` | `https://[nuestro-host]/[ambiente]/...` |
| Datos del Proveedor | — | RNC, Razón Social, Nombre Comercial de nuestra empresa |

Estas URLs son registradas en el directorio de servicios de DGII y son accesibles públicamente desde la OFV. Esto significa que **las URLs de nuestro SaaS deben estar disponibles y correctas desde el día de postulación del primer cliente**.

---

#### G. Pruebas de Comunicación (Pasos 8-11 — Track B) — Implicación para nuestro SaaS

En los Pasos 8 y 9, DGII envía e-CFs de prueba directamente al endpoint B2B del emisor. El emisor debe:
- Recibir el e-CF en `POST /fe/recepcion/api/ecf`
- Retornar un **ARECF firmado digitalmente**
- DGII valida que la respuesta sea un ARECF válido y firmado

En los Pasos 10 y 11, DGII envía ACECF de prueba al endpoint del emisor:
- Recibir en `POST /fe/aprobacioncomercial/api/ecf`
- Retornar `HTTP 200` (OK) o `HTTP 400` (Error)

> **Implicación para nuestro SaaS en Track A (clientes con proveedor):** Aunque en Track A los clientes NO pasan las pruebas de comunicación individualmente, **NOSOTROS** como proveedor sí debemos haberlas pasado cuando nos certificamos. Nuestros endpoints B2B deben funcionar correctamente en producción porque DGII los puede testear en cualquier momento.

---

#### H. Secuencias en CerteCF

- Rango disponible: **1 a 10,000,000** secuencias por tipo de e-CF
- Las secuencias **NO pueden reutilizarse** en diferentes intentos aunque el comprobante haya sido rechazado
- Si un e-CF resulta "Rechazado" en CerteCF, se debe usar una nueva secuencia para el reenvío

---

#### I. Al Finalizar (Paso 15) — Qué se habilita en la OFV del emisor

- Menú FE con: registro de contingencia, delegación, consulta de e-CF emitidos/recibidos/anulados
- Consulta de directorio electrónico
- Mantenimiento de directorio (donde se cargan las URLs de producción)
- URL OFV: `https://www.dgii.gov.do/ofv/login.aspx`

---

#### J. Hallazgos Críticos para Nuestro SaaS

1. **Nuestras URLs deben estar operativas desde la primera postulación del primer cliente.** El formulario registra nuestras URLs en DGII. Si están caídas durante las pruebas de simulación del cliente, el proceso se bloquea.

2. **Track A (con proveedor) elimina las Pruebas de Datos, RI y Comunicación.** Esto es el principal valor de ser proveedor certificado: nuestros clientes solo hacen simulaciones (1 por tipo), no 21+ comprobantes con datos DGII, no envío de PDFs, no pruebas de recepción B2B. Su certificación es significativamente más rápida.

3. **En Paso 2 (Pruebas de Datos — Track B), DGII suministra el set de datos en Excel.** El sistema debe generar los XML con exactamente los mismos campos y orden que el Excel de DGII. Si el e-CF resulta rechazado, hay que reiniciar la generación del set de datos con nueva secuencia.

4. **La Representación Impresa (RI) es validada por DGII en Paso 6 (Track B).** Los PDFs deben cumplir las especificaciones mínimas de formato, incluyendo la correcta conformación del QR. La RI es un bloqueo para avanzar en la certificación si se rechaza.

5. **Las secuencias CerteCF no son reutilizables.** El sistema debe tener un pool de secuencias de prueba dedicado. Dado que el rango va de 1 a 10M, hay amplio margen, pero el motor de secuencias debe permitir un punto de inicio configurable por ambiente.

6. **Aprobaciones Comerciales tienen prueba propia (Paso 3, Track B).** El set de datos viene de DGII. Las respuestas del sistema son "OK" o "Error/Incorrecto" — esto no es una validación de DGII sino de que el sistema genera el ACECF correctamente.

---

## 5.13 Proceso para Convertirse en Proveedor de Servicios FE Certificado

> **Fuente:** Respuesta oficial del foro DGII + Guía Básica Proveedor de Servicios FE (PDF oficial DGII)

Esta sección documenta el proceso que debemos completar NOSOTROS para operar como proveedor certificado. Es distinto al proceso de certificación de nuestros clientes (sección 5.12).

---

### A. Secuencia Obligatoria (no se puede saltar ningún paso)

```
1. Desarrollar el software de FE
        ↓
2. Certificarnos como emisores electrónicos (Track B — proceso completo de 15 pasos)
        ↓
3. Conseguir 3 primeros clientes y certificarlos (sin ser proveedor aún → usan Track B individualmente)
        ↓
4. Completar el formulario FI-GDF-017 presencialmente en la sede de DGII
        ↓
5. Esperar 10 días hábiles para respuesta de DGII
        ↓
6. DGII nos certifica como proveedor autorizado
        ↓
7. Nuevos clientes pueden usar Track A (proceso simplificado — solo simulaciones)
```

**Implicación crítica:** Los primeros 3 clientes hacen Track B (proceso completo de 15 pasos), NO Track A. El valor de ser proveedor certificado aplica solo al cliente #4 en adelante.

---

### B. Requisitos para la Solicitud de Certificación como Proveedor

Según el foro oficial DGII (respuesta oficial del área técnica):

| Requisito | Detalle |
|-----------|---------|
| **Formulario FI-GDF-017** | Presencialmente en sede DGII. No hay proceso digital disponible. |
| **RNC con actividad de TI** | El RNC de la empresa debe tener registrada actividad económica de tecnología. |
| **Ser emisor electrónico** | Debemos estar certificados como emisores electrónicos antes de solicitar. |
| **Mínimo 3 contribuyentes certificados** | Tres clientes ya certificados como emisores electrónicos usando nuestro software. |
| **Al día en obligaciones tributarias** | Sin deudas pendientes con DGII. |
| **Acceso a OFV** | Acceso activo a la Oficina Virtual DGII. |
| **Certificado Digital de Personas Físicas** | Certificado digital del representante legal de la empresa. |
| **Cumplir exigencias técnicas** | Ver sección C abajo. |

---

### C. Dos Modelos Operativos para Proveedores (Guía Básica DGII)

DGII define dos modelos de operación para proveedores de servicios FE:

#### Modelo 1 — Software instalado en infraestructura del cliente
- El proveedor desarrolla y entrega el software.
- **Los certificados digitales y la firma de e-CFs ocurren en la infraestructura del contribuyente (cliente).**
- El proveedor no custodia certificados de terceros.
- Menor complejidad regulatoria de custodia, pero el proveedor no controla el ambiente de ejecución.

#### Modelo 2 — SaaS (Software as a Service) ← **NUESTRO MODELO**
- El proveedor opera el software en su propia infraestructura.
- **Los certificados digitales y la firma de e-CFs ocurren en la infraestructura del proveedor.**
- El proveedor custodia los certificados P12 de sus clientes.
- **Implicación para nosotros:** El Vault de certificados (Sección 3) no es una optimización — es un **requisito regulatorio explícito** del Modelo 2. La seguridad y custodia de los certificados es responsabilidad directa del proveedor.
- DGII requiere que el proveedor garantice que el contribuyente pueda firmar sus e-CFs con su propio certificado digital, incluso en modelo SaaS.

---

### D. Tres Etapas de la Certificación como Proveedor (Guía Básica)

| Etapa | Nombre | Contenido |
|-------|--------|-----------|
| **1** | Solicitud | Presentación del FI-GDF-017 con todos los requisitos; verificación por DGII |
| **2** | Pruebas y Declaración Jurada | Pruebas técnicas que DGII ejecuta sobre el sistema del proveedor; firma de declaración jurada de cumplimiento |
| **3** | Certificación | DGII emite la certificación; el proveedor aparece en el listado oficial |

---

### E. Obligaciones Técnicas del Proveedor (Guía Básica)

DGII exige que el software del proveedor cumpla:

- **Elaboración, emisión y consulta de e-CFs** — el motor completo de generación de comprobantes.
- **Sincronización con los sistemas de DGII** — conectividad estable con los tres ambientes (TesteCF, CerteCF, eCF).
- **Criptografía:** confidencialidad, integridad, autenticidad y disponibilidad de la información fiscal.
- **Formatos estructurados** según especificaciones FE de DGII (XSD, esquemas de firma, etc.).

---

### F. Responsabilidades Continuas del Proveedor Certificado

Una vez certificados, DGII exige mantener permanentemente:

| Obligación | Descripción |
|------------|-------------|
| **Confidencialidad** | Garantizar confidencialidad de la información fiscal de los contribuyentes. |
| **Almacenamiento seguro** | Custodia segura de datos fiscales y certificados digitales. |
| **SLA de disponibilidad** | Disponibilidad mínima garantizada (nivel exacto según contrato con DGII). |
| **Help Desk técnico** | Asistencia técnica obligatoria para los contribuyentes clientes. |
| **Ley 32-23** | Responder ante uso indebido de los datos de contribuyentes según Ley de Protección de Datos Personal. |
| **Acceso a firma propia** | El contribuyente debe poder firmar sus e-CFs con su propio certificado, incluso en modelo SaaS. |

---

### F.2 Proceso de Solicitud para ser Emisor Electrónico (nuestros clientes)

> Fuente: foro oficial DGII 2024. Formulario: **FI-GDF-016**.

| Vía | Tiempo de respuesta | Detalles |
|-----|---------------------|---------|
| **OFV (online)** | **1 día laborable** ← usar siempre | Menú Solicitudes → "Solicitud para ser Emisor Electrónico" |
| Presencial | 10 días laborables | Centro de Asistencia Presencial / Administraciones Locales |
| Email | 10 días laborables | `facturacionelectronica@dgii.gov.do` — FI-GDF-016 escaneado |

**Flujo OFV post-aprobación:** DGII envía al buzón OFV del cliente una URL al "Portal de Certificación" donde establece su contraseña para autenticarse en TesteCF y CerteCF.

**Implicación para nuestro onboarding:** Siempre guiar al cliente por OFV. La habilitación llega en 24h. El email y presencial son solo backup.

---

### G. Hallazgos Críticos para Nuestra Empresa

1. **El Vault no es opcional — es un requisito regulatorio.** El Modelo 2 (SaaS) implica que custodiamos los certificados P12 de nuestros clientes. DGII exige garantizar seguridad y disponibilidad de esos certificados. La arquitectura con Vault (HashiCorp Vault o equivalente) es la respuesta técnica a ese requisito.

2. **El help desk no es un diferenciador — es un requisito.** DGII exige asistencia técnica a los contribuyentes como condición de mantener la certificación. Debemos tener un canal de soporte funcional desde el primer día como proveedor.

3. **Los primeros 3 clientes son la "inversión de entrada" al mercado proveedor.** No generan ingresos premium de Track A, pero son el prerequisito para llegar al Track A. Seleccionar clientes de perfil técnico que puedan navegar el Track B con relativa autonomía.

4. **La obligatoriedad de noviembre 2026 es un catalizador.** La regulación obliga a grandes y medianos contribuyentes desde noviembre 2026. Si logramos certificación como proveedor antes, podemos absorber clientes que buscan proveedor al último momento.

5. **Plazo real hasta Track A:** Desarrollo (1 mes) + Certificación propia emisor/Track B (variable, ~semanas) + 3 clientes Track B (~semanas cada uno) + trámite DGII (10 días hábiles) = mínimo 3-4 meses realistas antes de ofrecer Track A. Planificar con ese horizonte.

6. **La Ley 32-23 (Protección de Datos) aplica explícitamente.** Al custodiar datos fiscales de contribuyentes (incluyendo sus certificados digitales), somos responsables bajo la Ley 32-23. Esto tiene implicaciones para la política de privacidad, contratos con clientes y procedimientos de respuesta a incidentes.

---

## 6. Corte Day-One vs. Sobre-la-marcha

**Day one (bloqueante para emitir el primer e-CF real):**
- Auth con cache de token por tenant (semilla + firma + renovación proactiva).
- Recepción de e-CF (tipos 31, 32≥250k, 33, 34 como mínimo) + polling.
- Ruteo tipo+monto → RFCE vs Recepción normal para tipo 32.
- Asignación de secuencia con lock; liberación condicionada a `secuenciaUtilizada`;
  vencimiento 31-dic del año siguiente.
- Firma XML-DSig (SHA-256, preservewhitespace=false) + validación del certificado
  y del campo `SN` ANTES de firmar.
- Serializador: sin tags vacíos, escape de caracteres, orden de secciones correcto.
- Motor de redondeo con excepciones: 4 dec para PrecioUnitarioItem y TipoCambio,
  3 dec para Subcantidad, 2 dec para todo lo demás.
- Validación de tolerancia antes de firmar (anticipar "aceptado condicional").
- Motor de cálculo de `<MontoItem>` y todos los totalizadores del Encabezado.
- Lógica de `<FechaVencimientoSecuencia>`: solo aplica a tipos ≠ 32 y ≠ 34.
- Cálculo correcto de base imponible cuando ISC incluido (códigos 006-039).
- Validación de `<MontoTotal>` de NC ≤ `<MontoTotal>` del e-CF modificado.
- Validación de `<IndicadorNotaCredito>` para tipo 34 (regla de los 30 días).
- Ruteo de `<RNCComprador>` en tipo 32 (solo requerir si ≥ RD$250,000).
- `<FechaHoraFirma>` en GMT-4, ≤ fecha/hora actual.
- Contingencia tipo 1 (falta de conectividad, ventana 72h) — más probable en prod.
- Generación de QR + código de seguridad (dos variantes: e-CF normal vs RFCE).
- Máquina de estados: "aprobado por receptor pero rechazado por DGII".

**Puede ir sobre la marcha:**
- Endpoints B2B receptor (recibir e-CF de terceros).
- Anulación de rangos de secuencia.
- Aprobación Comercial (no aplica a todos los tipos).
- Consulta directorio / TrackIds (utilidades de soporte).
- Contingencia tipos 2 y 3.
- ISC e impuestos adicionales (según perfil de primeros clientes).
- Múltiples layouts de RI.
- Todos los 10 tipos de e-CF (arrancar con 31, 32, 33, 34 como mínimo).
- Subtotales Informativos (Sección C) — útil para PyMEs pero no bloqueante.
- Descuentos/Recargos globales (Sección D) — condicional a tipos de cliente.
- Paginación automática por layout de RI.
- Panel admin multi-tenant, anexos fiscales 606/607/IT-1.

## 7. Documentos de DGII procesados y pendientes

**Ya procesados (campo por campo):**
1. Descripción Técnica de Facturación Electrónica (v1.6, jun 2023)
2. Informe Técnico e-CF v1.0 (actualizado mar 2026)
3. **Formato Comprobante Fiscal Electrónico (e-CF) V1.0 — oct 2025** ✅ COMPLETO (sección 5.3)
4. **Formato Resumen Factura Consumo Electrónica v1.0 (RFCE)** ✅ COMPLETO (sección 5.4)
5. **Formato Acuse de Recibo v1.0 (ARECF)** ✅ COMPLETO (sección 5.5)
6. **Formato Aprobación Comercial v1.0 (ACECF)** ✅ COMPLETO (sección 5.6)
7. **Formato Anulación de e-NCF v1.0 (ANECF)** ✅ COMPLETO (sección 5.7)

8. **Firmado de e-CF (proceso técnico de firma digital)** ✅ COMPLETO (sección 5.8)
9. **Instructivo de Contingencia FE** ✅ COMPLETO (sección 5.9)

10. **Descripción Técnica Servicios DGII (v1.7, May 2026)** ✅ COMPLETO (sección 5.10)
11. **Descripción Técnica de Servicios Emisores Electrónicos (v1.7, Mayo 2026)** ✅ COMPLETO (sección 5.11)

12. **Proceso de Certificación para ser Emisor Electrónico con Proveedor de Servicios FE Certificado (Mayo 2026)** ✅ COMPLETO (sección 5.12 — Track A)
13. **Proceso de Certificación para ser Emisor Electrónico (Mayo 2026)** ✅ COMPLETO (sección 5.12 — Track B)

14. **Guía Básica Proveedor de Servicios FE + Foro oficial DGII** ✅ COMPLETO (sección 5.13 — cómo NOSOTROS nos certificamos como proveedor)

**Todos los documentos de DGII procesados.** ✅

## 8. Cómo continuar

Al abrir un nuevo chat dentro de este Project: pega este archivo como instrucciones
del Project o súbelo a la base de conocimiento. Las secciones 5.1-5.7 contienen los
hallazgos verificados contra los PDFs oficiales de todos los formatos. Las secciones
3, 4 y 6 contienen las decisiones de arquitectura tomadas.

**Todos los documentos de DGII han sido procesados.** ✅

**Próximos pasos:**
1. Corregir el `Plan Técnico Integral.txt` con todos los errores confirmados (15+ errores documentados en sección 9).
2. Construir el software (meta: ~1 mes). Priorizar lo bloqueante del Day-One (sección 6).
3. Certificarnos como emisores electrónicos (Track B — 15 pasos). Tener URLs B2B operativas antes de iniciar.
4. Conseguir los 3 primeros clientes y guiarlos por Track B. Son el prerequisito del formulario FI-GDF-017.
5. Tramitar la certificación como proveedor presencialmente en DGII (FI-GDF-017 + documentos, sección 5.13).
6. A partir del cliente #4: ofrecer Track A (onboarding simplificado — ventaja competitiva principal).
7. El Vault de certificados es un requisito regulatorio, no opcional (Modelo 2 / SaaS — sección 5.13-C).

## 9. Plan técnico previo (generado con IA, pre-validación) — evaluación

Emmanuel encontró un "Plan Técnico Integral" de 19 secciones (módulos 1-15, roadmap
6 fases/20 semanas, checklist de certificación) generado con IA antes de contrastar
los PDFs oficiales.

**Útil como esqueleto:** módulos y RFs numerados, roadmap de fases, estructura del
checklist de certificación, tabla `secuencias_ecf`, tabla de roles RBAC, catálogo de
alertas del panel de administración.

**Errores confirmados — NO implementar sin corregir:**

1. **Endpoints DGII incorrectos.** El plan usa `POST /fe/recepcion/api/ecf` como
   endpoint de envío a DGII — esa ruta es la que cada contribuyente EXPONE para recibir
   de otros (B2B). El endpoint real de envío a DGII es `/api/facturaselectronicas`.
   Los endpoints de consulta (Módulo 10) tampoco coinciden con la Descripción Técnica.

2. **Regla de tolerancia incorrecta.** El plan: ±1 peso global, rechazar localmente
   si se excede. Realidad: ±1 unidad por línea, tolerancia global=cantidad de líneas,
   y si se excede → DGII **acepta condicional** (no rechaza).

3. **Módulo de contingencia no conforme.** El plan trata contingencia como un solo
   flujo. Realidad: tres tipos con relojes distintos (72h / 15 días / 30 días).
   Falta la leyenda obligatoria en la RI y la notificación formal a DGII.

4. **Campos XML del Módulo 2 sin verificar** — ahora verificados en sección 5.3.
   En particular: `FechaVencimientoSecuencia` ausente en tipos 32 y 34, `MontoItem`
   puede ser cero en NC con corrección de texto, 4 decimales en `PrecioUnitarioItem`.

5. **Inconsistencia interna tipo 45**: excluido del envío a receptor en Módulo 4
   pero incluido en Módulo 5 para aprobación comercial — **RESUELTO**: el Formato
   ACECF v1.0 confirma que la aprobación comercial aplica a e-CFs previamente aceptados
   por DGII; el tipo 45 (Gubernamental) sí puede recibir ACECF. Revisar Módulo 4.

6. **RabbitMQ/SQS desde el día uno** — contradice la decisión de outbox pattern
   sobre Postgres/SQL Server (ver sección 3).

7. **Casing de tag XML `<RncEmisor>` vs `<RNCEmisor>`:** el ANECF usa `<RncEmisor>` (c
   minúscula) — diferente al resto de formatos que usan `<RNCEmisor>`. El serializador
   debe usar el casing exacto por documento; un modelo de dominio compartido con
   propiedad unificada debe serializarse con alias por formato.

8. **Lógica de anulación no modelada:** el plan técnico menciona anulación de rangos
   pero no modela la bifurcación ANECF vs Nota de Crédito (tipo 34). Esta lógica es
   obligatoria: si el e-CF fue enviado → Nota de Crédito; si no fue enviado → ANECF.

9. **`<TipoIngresos>` del RFCE no está en el modelo:** este campo (6 códigos) es
   exclusivo del RFCE y no tiene representación en el modelo de datos propuesto por el
   plan técnico. Debe añadirse como campo obligatorio en la entidad de emisión de
   facturas de consumo <DOP$250,000.

10. **Módulo de contingencia no distingue tipo 1 vs tipo 2:** El plan técnico trata la
    contingencia como un flujo único. En realidad: tipo 1 (falta de conectividad) NO
    requiere notificación a DGII — solo almacenar y reenviar en 72h con leyenda específica
    en el RI. Tipo 2 (imposibilidad técnica) SÍ requiere notificación obligatoria vía OFV,
    tiene un máximo de 15 días calendario, y al salir tiene 30 días para regularizar con
    e-CF que van SOLO a DGII (no al receptor). La leyenda del RI en tipo 1 es prescrita
    verbatim por DGII — no parafrasear.

11. **PHP: C14N exclusivo en lugar de estándar** — si la implementación PHP usa
    `$element->C14N(true, false)` (exclusive C14N) produce una firma que no verifica,
    aunque el proceso no lanza error. Debe usarse `$element->C14N(false, false)`.
    Revisar cualquier implementación PHP de firma en el código.

12. **Endpoints de consulta no disponibles en CerteCF** — el ambiente de certificación
    (CerteCF) no tiene los servicios `consultaestado`, `consultatrackids`, `anulacionrangos`
    ni `consultadirectorio`. El plan técnico asumía que los 3 ambientes tenían los mismos
    endpoints. Diseñar con lógica condicional por ambiente.

13. **Endpoints B2B y endpoints DGII son diferentes paths** — el plan técnico confundió
    el endpoint que cada contribuyente EXPONE para recibir e-CFs (`/fe/autenticacion/api/semilla`,
    `/fe/autenticacion/api/validacioncertificado`) con los endpoints de DGII para enviar
    (`/api/autenticacion/semilla`, `/api/autenticacion/validarsemilla`). Son estructuras
    completamente distintas: los B2B usan `/fe/` en el path, los de DGII usan `/api/`.

14. **QR del RI es URL completa construida con parámetros**, no solo el código de
    seguridad — el generador de RI debe construir la URL completa, con encoding correcto
    de fecha/hora. Dos variantes: `consultatimbre` (7 params, dominio ecf) para e-CF normal,
    `consultatimbrefc` (4 params, dominio fc) para RFCE. El plan técnico no distinguía estas dos.

**Estado**: el archivo `Plan Técnico Integral.txt` en disco está marcado como borrador
pre-validación. Se actualizará después de procesar todos los documentos de DGII.

## 10. Infraestructura, Stack y Base de Datos

Documento completo en artefacto publicado: https://claude.ai/code/artifact/ca89b512-cce7-411d-b935-902c065f9137

### 10.1 Cloud Provider — Decisión: Microsoft Azure

| Proveedor | Costo MVP/mes | Vault nativo | Managed Identity | Veredicto |
|-----------|--------------|--------------|------------------|-----------|
| AWS | ~$77 | ✓ Secrets Manager | ◐ IAM Roles | Descartado (costo) |
| **Azure** | **~$51** | **✓ Key Vault** | **✓ Nativo** | **✅ ELEGIDO** |
| DigitalOcean | ~$42 | ✗ No incluido | ✗ Variables entorno | Descartado (sin Vault nativo — requisito regulatorio) |

**Razón clave**: DigitalOcean descartado porque custodiar certificados P12 de clientes sin Vault nativo es un riesgo regulatorio para el Modelo 2 (SaaS). Azure gana sobre AWS por costo y por Managed Identity nativo (cero credenciales en código).

### 10.2 Stack Tecnológico — Definitivo

| Capa | Tecnología | Notas |
|------|-----------|-------|
| Backend API | ASP.NET Core 10 (C#) | OpenAPI nativo en .NET 10, sin NestJS |
| Background Service | Background Service (.NET) | Outbox worker, procesamiento async |
| XML Signing | System.Security.Cryptography.Xml | C14N standard (false,false) |
| Arquitectura | Clean Architecture | Domain/Application/Infrastructure/Service |
| Frontend | Next.js 15 + TypeScript + Tailwind CSS | Server Components |
| Base de datos | PostgreSQL 16 + EF Core 10 | Azure Flexible Server |
| Cache / locks | Redis 7 + StackExchange.Redis | Azure Cache for Redis |
| Secretos / certs | Azure Key Vault + Managed Identity | DefaultAzureCredential |
| Infra prod | Azure Container Apps + Azure Container Registry | Sin cluster Kubernetes |
| CI/CD | GitHub Actions | Build → ACR → Deploy Container Apps |
| Observabilidad | Azure Monitor + Application Insights | Logs, métricas, trazas |
| DNS / CDN | Cloudflare (free tier) | DDoS, DNS, protección endpoints B2B |
| Dev local | Docker + Docker Compose | API + worker + DB + Redis + Vault local |

**NestJS eliminado**: La recomendación de IA de usar NestJS para "mejor DX de OpenAPI" fue descartada — ASP.NET Core 10 tiene soporte nativo de OpenAPI sin librerías adicionales.

### 10.3 Base de Datos — Decisión: Azure Database for PostgreSQL Flexible Server

| Plataforma | Precio | Managed Identity | VNET | Cold-start | Soberanía datos |
|-----------|--------|-----------------|------|-----------|----------------|
| Supabase | $25/mes | ✗ | Solo Team | ✗ | ◐ US/EU |
| Neon | $19/mes | ✗ | ✗ Público | ✗ 500ms–2s | ✗ Solo US/EU |
| **Azure Flexible Server** | **~$30/mes** | **✓** | **✓** | **✓** | **✓ Configurable** |

**Factores decisivos**:
- Soberanía de datos: datos fiscales deben residir en región controlada
- Managed Identity: cero credenciales en código — requisito del modelo de seguridad
- Sin cold-start: el lock pesimista en secuencias NCF no puede tener latencia variable
- Mismo ecosistema Azure: Monitor, alertas y logs unificados

**Nota**: Supabase y Neon son válidos para entornos de staging/dev (branching de DB acelera el workflow).

**Configuración Flexible Server (producción)**:
```
SKU:        Standard_D2ds_v4 (2 vCores, 8GB RAM)
Storage:    128 GB premium SSD — auto-grow activado
PG version: 16
HA:         Zone-redundant (prod) / Single zone (staging)
Backup:     7 días PITR + geo-redundant
Extensions: uuid-ossp, pg_stat_statements, pgcrypto
Firewall:   Solo subnet de Container Apps — sin acceso público (VNET-only)
Pooling:    PgBouncer integrado, pool_mode=transaction, max_client_conn=200
```

### 10.4 Diseño de Esquema PostgreSQL

**Estrategia multi-tenant: Row-Level Security sobre schema compartido**

Un solo migration set, connection pool compartido. `tenant_id UUID NOT NULL` en cada tabla. Políticas RLS filtran automáticamente — la app inyecta `SET LOCAL app.tenant_id = '<uuid>'` al inicio de cada request.

**Tablas principales**:

```sql
-- Empresas clientes
tenants (id, rnc, razon_social, plan, estado, created_at)

-- Secuencias NCF por tipo — con LOCK PESIMISTA para evitar duplicados
secuencias_ecf (tenant_id, tipo, serie, siguiente, maximo, vencimiento, activo)
-- Uso: SELECT siguiente FROM secuencias_ecf WHERE ... FOR UPDATE;

-- Comprobantes emitidos — registro principal + XML firmado completo
comprobantes_ecf (
  id, tenant_id, tipo, numero, ncf, rnc_emisor, rnc_comprador,
  monto_total, fecha_emision, xml_firmado,
  estado_dgii,  -- pendiente|enviado|aceptado|aceptado_condicional|rechazado|contingencia
  trackid_dgii, fecha_envio_dgii, respuesta_dgii JSONB,
  contingencia, tipo_contingencia
)

-- Outbox Pattern — cola de envío a DGII y receptores
outbox (id, tenant_id, tipo, payload JSONB, estado, intentos, proximo_intento, error_ultimo)
-- Index: (estado, proximo_intento) WHERE estado IN ('pendiente','reintento')

-- Log de eventos — APPEND-ONLY, nunca UPDATE/DELETE (10 años retención)
eventos_ecf (id BIGSERIAL, tenant_id, comprobante_id, tipo_evento, payload JSONB, created_at)

-- Certificados P12 — solo metadata; el P12 real vive en Azure Key Vault
certificados_tenant (id, tenant_id, key_vault_name, thumbprint, subject_cn, valido_desde, valido_hasta, activo)
```

**Retención 10 años** (requisito ley tributaria RD):
- Particionamiento `PARTITION BY RANGE (fecha_emision)` en `comprobantes_ecf` y `eventos_ecf`
- Una partición por año — queries con filtro de fecha tocan solo la partición relevante
- `eventos_ecf` protegida con policy RLS que niega UPDATE/DELETE + role `fe_app` sin esos permisos
- Backup largo: `pg_dump` mensual → Azure Blob Storage cold tier → Lifecycle policy 10 años

**Por qué no SQL Server**: PostgreSQL tiene JSONB nativo (payloads DGII), RLS maduro, particionamiento declarativo y menor costo de licencia. EF Core 10 soporta ambos por igual.

---

## Sección 11 — Revisión de Stack e Infraestructura (post-análisis de costos)

### 11.1 Decisión: Cambio de Azure a Supabase + Railway

Azure resultó inviable para el MVP por costos: solo la base de datos (Azure Flexible Server uso general) cuesta ~$150/mes, llevando el total a $300–400/mes. Se tomó la decisión de migrar a un stack más económico manteniendo la misma arquitectura de código (C# + PostgreSQL + RLS).

**Stack seleccionado — Stack A (recomendado para lanzamiento):**

| Componente | Servicio | Costo estimado |
|-----------|---------|---------------|
| API + Worker (C# Docker) | Railway (Starter) | ~$20–25/mes |
| PostgreSQL 16 + RLS + Vault | Supabase Pro | $25/mes |
| Redis (sesiones/cache) | Upstash serverless | $0–5/mes |
| Dashboard Next.js | Vercel (free tier) | $0 |
| **TOTAL** | | **~$45–55/mes** |

**Alternativas documentadas:**
- Stack B: DigitalOcean completo (~$55–70/mes) con HashiCorp Vault self-hosted en Droplet $6
- Stack C: Hetzner VPS único (~$18–30/mes) — máximo ahorro, máxima gestión manual

**Gestión de certificados — Roadmap de Vault:**
- Fase 1 (0–15 clientes): Supabase Vault (pgsodium) — $0 adicional, válido para certificación DGII
- Fase 2 (~15–50 clientes): HashiCorp Vault self-hosted, Transit Secrets Engine, auto-unseal via KMS (~$6–15/mes)
- Fase 3 (50+ clientes): Azure Key Vault Premium o GCP KMS — $1/llave/mes HSM-backed

DGII no exige HSM explícitamente en la certificación inicial. Supabase Vault es aceptable para la Fase 1.

### 11.2 Nombre de la Empresa

**GreenSystems — "Soluciones tecnológicas a tu alcance"**

### 11.3 Comparativa de Lenguajes — Decisión Final: C# (ASP.NET Core)

Se evaluaron 4 opciones para el equipo (documentadas en artifact externo):

| Lenguaje | XMLDSig/C14N | Riesgo FE-DGII |
|---------|-------------|---------------|
| **C# (ASP.NET Core)** | ●●●●● — nativo, robusto | **BAJO** |
| PHP (Laravel) | ●●○○○ — bug C14N conocido (`C14N(true,false)` inválido) | ALTO |
| Python (FastAPI) | ●●●○○ — libs de terceros, frágil | MEDIO |
| TypeScript (NestJS) | ●●○○○ — xml-crypto con issues C14N | ALTO |

**Decisión confirmada: C# + ASP.NET Core**, dockerizado full. La percepción de "requiere VPS" para .NET es obsoleta — Railway y Render soportan imágenes Docker sin problema.

**Bug crítico PHP documentado**: `DOMDocument::C14N(true, false)` produce C14N exclusivo (incorrecto). Debe ser `C14N(false, false)` para C14N estándar que exige DGII.

---

## Sección 12 — Análisis Competitivo: FacturaYa

Se obtuvo acceso al panel administrativo de FacturaYa (panel.facturaya.com.do) y se hizo un análisis completo. Hallazgos clave:

### 12.1 Stack Técnico de FacturaYa

- **PHP 8.3.30 + MySQL 8.0.46** (Ubuntu 24.04)
- XMLs almacenados en filesystem (11MB de XML en disco, no en DB)
- Sin evidencia de Docker/contenedores — VPS directo
- Sin RLS nativo (MySQL no tiene RLS — el aislamiento multi-tenant es responsabilidad del código PHP)
- Logs: 45MB en disco local
- Disco 185GB, 41% usado, uptime 95.7 días

### 12.2 Modelo de Precios

- Plan visible: **MICRO — RD$1,200/mes + ITBIS, límite 300 documentos/mes**
- 10 empresas activas en el sistema
- Canal de partners/resellers construido pero con 0 partners activos
- Cobranza aparentemente manual (RD$0 cobrado en agosto 2026, RD$4,248 pendiente)

### 12.3 Fiabilidad — Punto Crítico

**Cola de Envíos: 45 fallidos / 17 completados sobre 62 ítems visibles (72% tasa de fallo)**

Causa identificada: endpoint `RecepcionECF` de DGII con **13,600ms de latencia** — las llamadas síncronas hacen timeout. Tienen circuit breakers implementados (parámetros `circuit_breaker_cooldown_minutos` y `circuit_breaker_umbral` en su config), pero no son suficientes.

Conteo por estado en monitor Servicios DGII:
- `AprobacionComercial`: 814ms
- `ConsultaTrackId`: 819ms  
- `RecepcionECF`: **13,600ms** ← causa principal de fallos
- `RecepcionRFCE`: 114ms
- `Semilla`: 699ms
- `ValidarSemilla`: 828ms

**Nuestra ventaja**: Outbox Pattern con retry exponencial + timeouts configurables por endpoint + notificación proactiva al cliente cuando un documento falla.

### 12.4 Seguridad

- **Tokens DGII almacenados en tabla key-value genérica** (api_config): claves como `dgii_token_10_Produccion`, `dgii_token_11_TestECF` — sin cifrado aparente
- Certificados gestionados sin vault cifrado
- 13 usuarios del panel incluyen emails de clientes finales — no hay portal separado cliente/admin

### 12.5 Funcionalidades que Tienen (para replicar/superar)

Dashboard KPIs, listado de documentos paginado con filtros, cola de envíos con estado por ítem, certificados por empresa con días de vencimiento, webhooks por empresa, recibidos B2B con acuse de recibo, monitor de salud de endpoints DGII, caja de pruebas por empresa (TestECF), cierre mensual, reportes exportables CSV, detección de duplicados configurable (modo: bloquear/observar, ventana en minutos), circuit breaker.

### 12.6 Lo que No Tienen (nuestras oportunidades)

- Portal de cliente separado del panel admin
- API pública documentada
- Vault cifrado para secrets
- Multi-tenant real a nivel DB (RLS)
- Retry con backoff exponencial
- Notificaciones proactivas al cliente
- Onboarding self-serve
- Cobro automatizado / billing integrado
- Audit log inmutable por tenant
- Status page público de endpoints DGII

**Vista de cliente de FacturaYa contiene**: dashboard / documentos / recibidos B2B / certificados / webhooks / mi empresa / cambiar contraseña / cola de envíos / caja de pruebas.

---

## Sección 13 — XSD Oficiales DGII

### 13.1 Archivos Disponibles

Ubicación en el proyecto: `C:\workplace\FE_DGII\XSD\`

| Archivo | Tamaño | Descripción |
|--------|--------|-------------|
| `e-CF 31 v.1.0.xsd` | 123KB | Crédito Fiscal — el más complejo, base para todos |
| `e-CF 32 v.1.0.xsd` | 123KB | Consumo |
| `e-CF 33 v.1.0.xsd` | 124KB | Nota de Crédito |
| `e-CF 34 v.1.0.xsd` | 122KB | Nota de Débito |
| `e-CF 41 v.1.0.xsd` | 111KB | Compras |
| `e-CF 43 v.1.0.xsd` | 96KB | Gastos Menores |
| `e-CF 44 v.1.0.xsd` | 114KB | Regímenes Especiales |
| `e-CF 45 v.1.0.xsd` | 122KB | Gubernamental |
| `e-CF 46 v.1.0.xsd` | 116KB | Exportaciones |
| `e-CF 47 v.1.0.xsd` | 101KB | Pagos al Exterior |
| `ACECF v.1.0.xsd` | 3.6KB | Aprobación Comercial (respuesta DGII) |
| `ANECF v.1.0.xsd` | 5.2KB | Anulación de e-NCF |
| `ARECF v1.0.xsd` | 2.9KB | Resumen Aprobación |
| `RFCE 32 v.1.0.xsd` | 15KB | Recepción Facturas Compras/Gastos (B2B) |
| `Semilla v.1.0.xsd` | 487B | Autenticación (semilla + fecha) |

Formatos PDF oficiales en: `C:\workplace\FE_DGII\Formatos_ECF\`

### 13.2 Estructura del XSD (e-CF 31 como referencia)

```
ECF
└── Encabezado
    ├── IdDoc (TipoeCF, eNCF, FechaVencimientoSecuencia, TipoPago, TablaFormasPago...)
    ├── Emisor (RNCEmisor, RazonSocialEmisor, DireccionEmisor, FechaEmision...)
    ├── Comprador (RNCComprador, RazonSocialComprador...)
    ├── InformacionesAdicionales (embarque, bultos, pesos...)
    ├── Transporte
    └── Totales (MontoGravadoTotal, ITBIS1/2/3, MontoTotal, ValorPagar...)
└── DetallesItems
    └── Item[] (NumeroLinea, CantidadItem, PrecioUnitarioItem, MontoItem...)
```

**El orden de elementos es mandatorio** (xs:sequence) — crítico para C14N correcto.

### 13.3 Plan de Uso en el Proyecto

1. **EmbeddedResource**: incluir todos los XSDs en el proyecto C# como recursos embebidos
2. **Validador runtime**: `XmlSchemaSet` + `XmlReader` valida cualquier XML generado antes de enviarlo a DGII
3. **Modelos C# manuales**: diseñar `record` types limpios informados por el XSD (no generación automática — código generado de anonymous complex types anidados es ilegible)
4. **Suite de tests**: un test por tipo de ECF que genera XML mínimo válido y lo valida contra el XSD oficial
5. **Atributos de validación**: tipos del XSD (`RNCValidationType`, `AlfNum150Type`, `Decimal18D1or2...`) mapean a DataAnnotations y FluentValidation

---

## Sección 14 — Estado Actual y Próximos Pasos

### 14.1 Estado al inicio del siguiente chat

- Decisiones técnicas 100% tomadas
- Stack definido: C# + ASP.NET Core + Supabase + Railway + Vercel
- Schema PostgreSQL diseñado (ver Sección 10.4)
- XSDs oficiales disponibles en `C:\workplace\FE_DGII\XSD\`
- Análisis competitivo completo de FacturaYa
- **Sin RNC de producción** — trabajar con TestECF de DGII
- **Sin certificado de producción** — TestECF tiene certificados de prueba propios

### 14.2 Orden de Trabajo Recomendado

1. Crear proyecto en Supabase → correr migrations del schema (tenants, secuencias_ecf, comprobantes_ecf, outbox, eventos_ecf, certificados_tenant con RLS completo)
2. Crear solución ASP.NET Core con estructura de carpetas (API / Domain / Infrastructure / Workers)
3. Incrustar XSDs como EmbeddedResource + implementar validador runtime
4. Construir modelos C# para e-CF 31 (Crédito Fiscal) — el más complejo
5. Implementar serializer XML respetando el orden xs:sequence del XSD
6. Implementar XMLDSig con C14N **estándar** (`false, false`) — componente más crítico
7. Conectar con ambiente TestECF de DGII (autenticación Semilla → Token → Envío)
8. Implementar Outbox Pattern con worker de reintentos
9. Expandir a los demás tipos de ECF

### 14.3 Credenciales TestECF DGII

DGII provee RNCs y certificados de prueba para el ambiente TestECF. Endpoints:
- Semilla: `https://ecf.dgii.gov.do/testecf/autenticacion?wsdl`
- Recepción: `https://ecf.dgii.gov.do/testecf/recepcion?wsdl`
- Aprobación Comercial: `https://ecf.dgii.gov.do/testecf/recepcion?wsdl`
- Consulta TrackId: `https://ecf.dgii.gov.do/testecf/consultaresultado?wsdl`

La latencia de `RecepcionECF` en TestECF es ~13,600ms — diseñar todos los timeouts y el Outbox asumiendo esa latencia desde el inicio.
