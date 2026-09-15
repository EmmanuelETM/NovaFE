# Fechas y horas

## Regla

- **Almacenamiento y lógica interna: instantes UTC.** `DateTimeOffset` en el
  dominio (nunca `DateTime`), columnas `timestamp with time zone`. Los
  interceptores estampan `timeProvider.GetUtcNow()`.
- **Hora dominicana solo en los bordes.** `America/Santo_Domingo` es UTC-4 fijo
  (RD no usa horario de verano desde el 2000).

`timestamptz` en PostgreSQL **no guarda la zona horaria** — guarda un instante
UTC. El offset se descarta al escribir. Por eso "guardar hora dominicana" no es
una opción real ahí, y tampoco hace falta: un instante UTC es inequívoco,
ordenable y comparable con las fechas de la DGII (que traen offset).

## Los bordes

| Borde | Qué hace | Herramienta |
|---|---|---|
| **API (salida)** | Todo `DateTimeOffset` se serializa con offset `-04:00`. Mismo instante, se ve en hora dominicana; coherente con la RI, y el front no tiene que convertir. | `DominicanDateTimeOffsetConverter`, registrado en `JsonSettings.Bulletproof` |
| **API (entrada)** | Acepta cualquier offset o `Z`. | idem (`Read` = `reader.GetDateTimeOffset()`) |
| **Serialización a la DGII** | `<FechaHoraFirma>` etc. en `dd-MM-yyyy HH:mm:ss`, hora dominicana, `≤ ahora` | `DominicanTimeZone.ToDateTimeString(instant)` |
| **Representación Impresa** | Fechas legalmente en hora dominicana | `DominicanTimeZone.ToDate(String)` |
| **Aritmética de calendario** | Vencimiento de secuencias e-NCF (31-dic del año siguiente), regla de 30 días de las NC, plazos de contingencia M11 Tipo 2/3 (15 d / 30 d — sin construir, ver `docs/contingency.md`) | `timeProvider.GetDominicanToday()` — hacerla en fecha local, no en UTC (si no, te desvías un día cerca de medianoche) |
| **Aritmética de instante** | Contingencia M11 Tipo 1: la ventana de 72h es desde que se restablece la conexión, no una fecha de calendario — `timeProvider.GetUtcNow() + TimeSpan.FromHours(72)`, no `GetDominicanToday()`. No hay un reloj explícito construido; el reintento sin dar por perdido mientras dure la contingencia la cumple en la práctica (`docs/contingency.md`) | `timeProvider.GetUtcNow()` |

## API

`src/Domain/Common/`:

- `DominicanTimeZone` — `Zone`, `ToLocal(instant)`, `LocalDate(instant)`,
  `ToDateTimeString(instant)`, `ToDateString(instant)`, y los formatos
  `DateTimeFormat` / `DateFormat`. Con fallback a UTC-4 fijo si el contenedor no
  trae base de datos de zonas horarias.
- `TimeProvider` extensiones: `GetDominicanNow()`, `GetDominicanToday()`.

## Al firmar (cuando se construya el serializador)

`<FechaHoraFirma>` debe ser `≤` la hora del sistema **de la DGII**. Si nuestro
reloj va un poco adelantado, la firma se rechaza. Conviene restar ~1-2 segundos
al instante de firma como colchón.
