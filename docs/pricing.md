# Planes y precios

> Documento de estrategia. Los números son un punto de partida; hay que
> validarlos contra el volumen real que emitan los primeros clientes. La DGII
> **no** regula cuánto puede cobrar un proveedor de servicios FE — solo exige
> cumplir sus especificaciones.

## Modelo

**Suscripción por niveles + consumo medido.** No cobro puro por documento
(ingresos impredecibles, los clientes lo optimizan) y **nunca** un porcentaje del
monto facturado (se percibe como abusivo en infraestructura de cumplimiento).

**Se mide una sola cosa:** e-CF **aceptado** por la DGII (las notas de crédito y
débito cuentan; el RFCE de una factura de consumo <RD$250k cuenta como uno). Los
rechazos no se cobran — no queremos penalizar la confianza en el sistema. Todo lo
demás (ARECF, ACECF, recepción B2B entrante, consultas, webhooks, RI/PDF) va sin
medir, con límites blandos generosos.

Por qué así: el costo marginal de un e-CF es casi cero (cómputo + una llamada a la
DGII + almacenamiento). Los costos reales son fijos: desarrollo, certificación y
**mesa de ayuda** (requisito regulatorio). El precio se fija por **valor**: desde
noviembre de 2026 el e-CF es obligatorio, y la alternativa del cliente es
construirlo (RD$500K–2M+ y mantenimiento perpetuo) o irse con la competencia.

## Niveles

| Nivel (clave interna) | Nombre comercial | RD$/mes | e-CF/mes incluidos | Excedente RD$/e-CF | Soporte | SLA | Para quién |
|---|---|---|---|---|---|---|---|
| `Developer` | Developer | Gratis | 100 (casi todo TestECF) | tope duro | Comunidad / Discord | — | Devs integrando, microemisores |
| `Starter` | Emprendedor | 1,200 | 400 | 5.00 | Email 48h | best-effort | Colmados, freelancers, negocios pequeños |
| `Business` | Negocio | 4,500 | 2,500 | 3.50 | Email 24h + chat | 99.5% | PyMEs — restaurantes, clínicas, servicios (**centro de ganancia**) |
| `Corporate` | Corporativo | 15,000 | 20,000 | 2.00 | Prioritario + teléfono | 99.9% + créditos | Grandes y medianos contribuyentes |
| `Enterprise` | Empresarial | A convenir | 50,000+ | negociado | Onboarding dedicado | A convenir | Cadenas de retail, utilities, white-label |

- Todos los precios **+ 18% de ITBIS**.
- **Facturación anual:** se pagan 10 meses (2 gratis).
- Emitimos nuestras propias facturas con e-CF (dogfooding).

### Notas por nivel

- **Developer** es un embudo y un foso, no un producto. Tope duro, exige un RNC
  real, casi todo TestECF. Captura microempresas antes de que crezcan y deja que
  los devs de ERPs construyan contra nosotros.
- **Emprendedor** iguala el precio de FacturaYa (RD$1,200 / 300 docs). No
  competimos por precio; ganamos el argumento con "sí llega y no te multan".
- **Negocio** es deliberadamente la opción obvia para la PyME mediana. Aquí vive
  la mayor parte del ingreso. RD$4,500 es trivial frente a una falla de
  cumplimiento.
- **Corporativo/Empresarial**: land-and-expand. El Empresarial puede incluir
  infraestructura dedicada, white-label y retención/exportación a medida.

## Canal de socios (reseller / white-label)

El multiplicador de volumen real. Firmas de contadores y proveedores de ERP traen
20–50 clientes cada uno.

- Precio mayorista **RD$2–3 por e-CF**, con un mínimo mensual (~RD$5,000).
- El socio fija su propio precio de venta y se queda el margen.
- FacturaYa construyó la plomería y tiene **0 socios activos** — hay que ir a
  hablar directamente con los contadores medianos y los ERPs locales.
- Un socio = muchos clientes sin que la carga de soporte escale igual.

## Servicios de una sola vez

- **Asistencia de certificación (Track B):** acompañar a un cliente por el
  proceso de 15 pasos de la DGII. **RD$25,000–60,000** una sola vez. Es ingreso de
  consultoría hoy; cuando seamos proveedor certificado, los clientes Track A lo
  reciben incluido — y eso pasa a ser el argumento de venta principal.
- **Ambientes/sandbox adicionales**, exportación histórica masiva, página de
  estado premium con créditos de SLA: add-ons.

## Lanzamiento

- **Primeros ~5 clientes (los pilotos Track B):** gratis 12 meses + 50% de por
  vida, a cambio de logo, llamada de referencia y caso de estudio. Son la
  inversión para nuestra certificación como proveedor, no ingreso.
- **Publicar la página de precios abierta.** FacturaYa la esconde; MSeller la
  muestra a medias. La transparencia es señal de confianza para quien compra
  cumplimiento.
- **Grandfathering:** los clientes tempranos conservan su precio cuando subamos
  tarifas.
- **Manejo proactivo del excedente:** auto-upgrade + aviso antes de que llegue la
  factura. Ya estamos construyendo notificaciones proactivas — se usa eso en vez
  de facturas sorpresa.

## Números aproximados — año 1

30 clientes de pago, promedio ~RD$3,500/mes → ~RD$1.26M/año, margen bruto ~85%.
Cubre infra + una persona de soporte para un equipo lean de 4. Año 2 con ~150
clientes + 3–4 socios activos aportando volumen → RD$500K–1M/mes. La economía
unitaria es excelente pasando ~50 clientes; lo difícil es la adquisición antes de
noviembre de 2026 y sobrevivir la fase piloto.

## ¿Tabla de planes en la base de datos?

**Ahora: no. Más adelante: sí.**

Hoy `TenantPlan` es un smart enum en el dominio
(`Developer/Starter/Business/Corporate/Enterprise`). Es suficiente: es una
etiqueta en el tenant, no hay medición ni facturación todavía, y los pilotos van
todos con precio de fundador. Meter una tabla ahora es YAGNI — y los brackets de
precio ni siquiera están validados.

**Cuando se construya el módulo de medición y facturación** (su propio vertical
slice, fuera del alcance day-one de la DGII), el diseño correcto es:

- **`plans`** — catálogo editable por operaciones sin deploy: `key`, precio
  mensual, e-CF incluidos, tarifa de excedente, SLA %, nivel de soporte, feature
  flags, `active`, fechas de vigencia. Se siembra desde una migración.
- **`tenant_subscriptions`** — tenant → plan, **con overrides**: volumen incluido
  a medida, precio a medida, fin de trial, % de descuento, período de facturación
  actual, estado. Necesario porque el Empresarial es "a convenir" y los clientes
  de fundador tienen tratos hechos a mano.
- **La aplicación aplica los límites efectivos** (`override ?? plan`), nunca por
  el nombre del plan.

Por qué la tabla eventualmente: los precios y límites cambian seguido
(promociones, grandfathering, descuento anual, deals negociados). Hardcodear eso
convierte cada experimento de precio en un deploy y hace imposible un cliente
"Negocio pero con 5,000 incluidos porque lo negociamos".

## Pendientes de validar

1. **Distribución de volumen real** — ajustar los brackets de e-CF incluidos con
   lo que emitan los pilotos. La *estructura* de niveles es correcta; los umbrales
   exactos necesitan una ronda de datos reales.
2. Elasticidad de precio en el nivel Negocio — probar RD$4,500 vs RD$5,500 con los
   primeros prospectos de pago.
