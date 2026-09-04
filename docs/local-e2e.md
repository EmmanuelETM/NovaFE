# Probar el pipeline completo en local (sin certificado real de la DGII)

Todo el flujo —perfil de emisor → asignar secuencia → armar → calcular → firmar →
XSD → enviar → polling → estados → retry— corre contra un **simulador de la DGII**
y un **certificado autofirmado**. Lo único que no es real es que la DGII de verdad
no aceptaría esa firma.

Por qué funciona: `Certificate.Issue` solo exige que el certificado tenga clave
privada, esté vigente y que su `SERIALNUMBER` sea el RNC del contribuyente — no
que lo emita la DGII. Y la firma autofirmada valida contra el XSD (`<Signature>`
va con `processContents="skip"`).

## 1. Arrancar

```bash
cp .env.dgii-sim.example .env
docker compose --profile dgii-sim up --build
```

Levanta: API (`http://localhost:8080`), PostgreSQL (`5432`) y `dgii-sim`
(WireMock, admin en `http://localhost:8088/__admin`). El `.env` apunta
`Dgii__*BaseUrl` al simulador y baja la primera consulta de estado a 2 s.

## 2. Onboarding en un solo paso

```bash
curl -sX POST http://localhost:8080/api/v1/dev/sandbox \
  -H 'Content-Type: application/json' -d '{}'
```

Registra un contribuyente con RNC aleatorio, le pone perfil de emisor, le carga
rangos de secuencia (tipos 31/32/33/34) y le sube un certificado autofirmado.
Devuelve:

```json
{ "tenantId": "0194...", "rnc": "512345678", "environment": "Test", "apiKey": "nfe_…", ... }
```

Guarda el `tenantId` y el `apiKey`. Para emitir usa el header `X-API-Key: <apiKey>`
(o, en local, `X-Tenant-Id: <tenantId>` — ver `docs/api-auth.md`). Los ejemplos de
abajo usan `X-Tenant-Id` por brevedad. (Opcional:
`{"rnc":"...","environment":"Cert","sequenceTypes":[31]}`.)

## 3. Emitir un e-CF

```bash
curl -sX POST http://localhost:8080/api/v1/ecf \
  -H 'Content-Type: application/json' \
  -H 'X-Tenant-Id: <tenantId>' \
  -d '{
    "type": 31,
    "incomeType": "01",
    "buyer": { "name": "Cliente de Prueba SRL", "rnc": "131880681" },
    "payment": { "condition": "cash", "methods": [{ "type": "cash", "amount": 2360 }] },
    "lines": [{ "name": "Consultoría", "kind": "service", "quantity": 1,
                "unitPrice": 2000, "itbisRate": 1, "unitOfMeasure": "43" }]
  }'
```

Con el simulador en su estado por defecto (`codigo: 1`) la respuesta llega con
**`"status": "accepted"`**, `trackId`, `dgiiProcessedAt`… — el fast-path lo
resolvió dentro del propio request.

```bash
curl -s "http://localhost:8080/api/v1/ecf/<id>" -H 'X-Tenant-Id: <tenantId>'
curl -s "http://localhost:8080/api/v1/ecf/<id>/xml" -H 'X-Tenant-Id: <tenantId>'
```

## 4. Probar los otros caminos

Edita `dev/dgii-sim/mappings/consulta-resultado.json` → `"codigo"`:

| valor | resultado |
|---|---|
| `1` | aceptado |
| `2` | rechazado (el e-NCF queda quemado) |
| `3` | en proceso — el fast-path devuelve `submitted`, luego el worker lo resuelve (o llega a `review` tras el ladder) |
| `4` | aceptado condicional |

Luego `docker compose restart dgii-sim` y vuelve a emitir.

- **DGII "caída"**: edita `recepcion-ecf.json` → `"status": 503`, reinicia. El
  `POST /ecf` devuelve `signed` y el worker reintenta con backoff hasta `failed`.
- **Gateway sin TrackId**: `recepcion-ecf.json` → `"trackId": ""`. → `failed`
  directo (no se reintenta).
- **Reintentar un `failed`/`review`**: arregla el mapping y
  `POST /api/v1/ecf/<id>/retry` con el `X-Tenant-Id`.

## 5. Inspeccionar

- e-CF del tenant: `GET /api/v1/ecf?status=rejected` (u otro estado).
- Peticiones que recibió el simulador: `http://localhost:8088/__admin/requests`.
- Base de datos: `docker compose --profile tools --profile dgii-sim up` añade
  pgweb en `http://localhost:8081`.

## 6. Reiniciar de cero

```bash
docker compose --profile dgii-sim down -v   # borra también los volúmenes
```

## Notas

- `POST /api/v1/dev/sandbox` y `GET /api/v1/dev/sandbox/certificate?rnc=…` solo
  existen en `Development` (`[DevelopmentOnly]`).
- El certificado autofirmado sirve para cualquier ambiente (`Test` / `Cert`
  / `Production`) porque el simulador no valida la firma. Contra la DGII real hace
  falta el certificado emitido por una CA autorizada y registrado en el portal.
- Para apuntar a la DGII real otra vez: borra el `.env` y `docker compose up`.
