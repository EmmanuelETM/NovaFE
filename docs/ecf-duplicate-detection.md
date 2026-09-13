# Detección de e-CF duplicados por huella

Capa adicional de dedup, separada de las dos que ya existen:

1. **`Idempotency-Key`** (header) → `IIdempotencyStore` — replay exacto del
   mismo cuerpo con la misma clave.
2. **`internalNumber`** (`<NumeroFacturaInterna>`) → un comprobante por
   `(tenant, internalNumber)`.

Ambas exigen que el **cliente** mande la clave/número correctos. Esta capa es
para cuando no los manda (o los manda distintos por error) pero el
comprobante es, en la práctica, el mismo — típicamente un doble clic o un
reintento manual sin idempotencia.

## La huella

Comprador (RNC/cédula) + tipo de e-CF + monto total + fecha de emisión +
ambiente (Test/Cert/Prod). Todas columnas que `issued_ecf` ya guarda
(`BuyerRnc`, `Type`, `MontoTotal`, `IssueDate`, `Environment`) — no hay tabla
ni columna nueva, solo un índice de soporte
(`src/Infrastructure/Ecf/EfCore/EcfConfiguration.cs`).

**El RNC/cédula del comprador es obligatorio para participar.** Sin él, no se
calcula huella ni se compara nada — el e-CF se emite normal. Esto es
deliberado, no una limitación de v1: una factura de consumo bajo DOP 250,000
no necesita identificar al comprador (RF-03, `EcfErrors.BuyerIdentificationRequired`),
y es común que varias personas paguen el mismo monto en minutos sin que sean
el mismo comprobante — inscripciones, entradas, cualquier venta al público
con un precio fijo. Usar el *nombre* del comprador como sustituto no
funciona: algunos emisores lo llenan con el nombre real, otros ponen un
literal genérico (p. ej. "Consumidor Final") en **todas** las facturas de
consumo sin excepción, y no hay forma confiable de distinguir un caso del
otro sin mantener una lista de literales — que nunca está completa y varía
por negocio. Por eso la huella exige un identificador fiscal real, y sin él
simplemente no participa.

## Modos (`ecf.duplicate_detection_mode`)

| Modo | Comportamiento |
|---|---|
| `off` (default) | No hace nada. Ni siquiera consulta. |
| `observar` | Emite igual, pero encola `ecf.duplicate_suspected` (ver `docs/webhooks.md`) si hay match. |
| `bloquear` | Rechaza con `409 Ecf.DuplicateSuspected` si hay match. **No** firma ni persiste — el e-NCF ya asignado se quema (mismo trade-off que cualquier otro rechazo post-asignación; v1 no tiene pool de secuencias liberadas). |

Default `off` a propósito: es comportamiento nuevo que podría rechazar
negocio legítimo si un operador lo prende sin pensarlo primero.

## Ventana (`ecf.duplicate_detection_window`)

Default 5 minutos (mínimo 30s, máximo 1h). Pensada para doble-clic o
reintento accidental — no para "la misma compra otro día". Una ventana corta
minimiza los falsos positivos incluso antes de considerar el filtro de RNC.

## Dónde corre

`IssueEcfUseCase` — justo después de armar el `EcfDocument` (ya se conoce el
monto fiscal exacto) y antes de firmar. El monto solo se conoce después de
asignar la secuencia (Módulo 7), igual que cualquier otro fallo de armado de
documento o de firma en este pipeline.

## Qué NO hace

- No reemplaza `Idempotency-Key` ni `internalNumber` — son ortogonales.
- No mira las líneas de detalle, solo el monto total.
- No es configurable por tenant — es una sola política de plataforma.
- No aplica a tipos/comprobantes sin comprador identificado — ver arriba.
