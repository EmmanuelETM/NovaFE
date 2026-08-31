# URL de verificación del timbre QR

Parte de **Módulo 9** (Representación Impresa + código QR), traída adelante porque
sus entradas son justo lo que produce la firma (Módulo 3). El resto de M9 (PDF
carta/POS, bitmap del QR, layout, leyenda de contingencia) sigue pendiente.

`EcfVerificationUrl.For(document, environment, securityCode, signedAt)`
(`src/Domain/Ecf/`) — función pura. La consume `EcfSigner`, que la expone como
`SignedEcf.QrUrl`.

## Qué construye

El QR de la RI **no** es el código de seguridad: es la URL completa de
`consultatimbre` en los servidores de la DGII. Quien recibe la RI la escanea y la
DGII le muestra el estado del e-CF (aceptado / rechazado / no encontrado).

Dos variantes (contexto DGII §K/§L):

| | e-CF normal | RFCE (tipo 32 &lt; DOP 250 000) |
|---|---|---|
| Host + ruta | `ecf.dgii.gov.do/{seg}/consultatimbre` | `fc.dgii.gov.do/{seg}/consultatimbrefc` |
| Parámetros | `rncemisor`, `rnccomprador`, `encf`, `fechaemision`, `montototal`, `fechafirma`, `codigoseguridad` (7) | `rncemisor`, `encf`, `montototal`, `codigoseguridad` (4) |

`{seg}` = `DgiiEnvironment.UrlSegment` (`testecf` / `certecf` / `ecf`).

- **`rnccomprador`**: RNC/cédula del comprador; si no tiene, el
  `IdentificadorExtranjero`; si tampoco (tipo 43, sin bloque comprador), vacío.
- **`fechafirma`**: la `FechaHoraFirma` del XML — `dd-MM-yyyy HH:mm:ss` en hora
  dominicana (UTC-4), RF-09.2.
- **`codigoseguridad`**: los 6 primeros caracteres del `SignatureValue` Base64. Se
  preserva tal cual (sensible a mayúsculas); `+` `/` `=` quedan percent-encoded.
- Cada valor pasa por `Uri.EscapeDataString`: espacio → `%20`, `:` → `%3A`. Los
  guiones y puntos de fechas y montos son *unreserved* y quedan literales.

## Sin confirmar contra la DGII real

Los docs de la DGII son inconsistentes en el detalle; esto está construido a
especificación pero necesita un envío/escaneo real para cerrar:

1. **Formato de `montototal`** — acá van 2 decimales fijos (`0.00`). El
   `<MontoTotal>` del XML, en cambio, no lleva ceros de más. Si la DGII compara
   contra el valor almacenado, podría no cuadrar.
2. **Encoding de `fechaemision`** — una tabla del contexto sugiere encodear `-` y
   `.`; el ejemplo concreto de la DGII no lo hace. Se sigue el ejemplo (RFC 3986:
   `-` `.` son *unreserved*).
3. **Caso del ejemplo** (`encf=e31…`, `codigoseguridad=dcp79q` en minúsculas) —
   se asume que es formateo del documento, no un requisito; el e-NCF va en
   mayúscula y el código de seguridad respeta el case del Base64.

Ver `C:\workplace\FE_DGII\contexto-proyecto-fe-dgii.md` §K/§L y §9-C, y el Plan
Técnico §Módulo 9.
