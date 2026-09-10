# Migrar a un VPS

Hoy NovaFE se despliega en **Azure Container Apps** + **Azure Key Vault**, contra
**Neon**. Nada de eso está soldado al código (ver [`deployment.md`](deployment.md)
§"Los puntos de swap"). Este documento es el runbook para mover la API a un
**VPS** (un servidor Linux con Docker: Hetzner, DigitalOcean, OVH, Contabo, un
bare-metal…), con `docker compose` + un reverse proxy.

El dashboard (`web/`, Vercel) es un proyecto aparte; su migración está en la
sección [Dashboard](#dashboard-web) al final.

---

## Qué cambia y qué no

### No se toca una línea de código

| Pieza | Por qué ya es portable |
|---|---|
| **Compute** | La imagen (`src/Service/Dockerfile`) es un contenedor ASP.NET estándar. El único guiño a un proxy es `UseForwardedHeaders` (`Program.cs`) — estándar detrás de **cualquier** reverse proxy. Los probes `/health/live` y `/health/ready` son genéricos (`HealthCheckExtensions.cs`). |
| **Base de datos** | Cualquier PostgreSQL 16+. Se elige con `ConnectionStrings:Default` + `Database:Provider` (`postgres` \| `neon`). |
| **Outbox, idempotencia, lock de secuencias** | PostgreSQL (`FOR UPDATE SKIP LOCKED` / advisory locks). Sin broker. |
| **Caché** | `IDistributedCache` en memoria. Sin Redis (ver [`redis.md`](redis.md)). |
| **Motor de settings runtime** | Poll del contador de generación en Postgres + `LISTEN/NOTIFY` (ver [`configuration.md`](configuration.md)). Multi-instancia sin infra extra. |
| **Observabilidad** | OTLP genérico (`Observability:OtlpEndpoint`); vacío = no exporta. Logs a `stdout` vía Serilog. |
| **Migraciones** | `RUN_MIGRATIONS_AND_EXIT=true` + advisory lock en `DatabaseInitializer`. El mecanismo ya es agnóstico; hoy lo dispara un Container Apps Job, en el VPS será un `docker run` de un solo uso. |
| **KEK del vault de certificados** | `CertificateVault:Provider` — `local` (KEK en variable de entorno, **default**) o `azure-key-vault`. Cambiar de proveedor es config, no código (ver [`certificates.md`](certificates.md)). |

### Se reemplaza (todo es infra, ninguna es de la aplicación)

| Artefacto | Hoy | En el VPS |
|---|---|---|
| `deploy/main.bicep` | ~330 líneas de recursos Azure | `docker-compose.prod.yml` (§5) |
| `deploy/README.md` | comandos `az …` | este documento |
| Terminación TLS + enrutado | Ingress de Container Apps (Envoy) | Caddy / nginx / Traefik (§6) |
| Ejecutor de migraciones | Container Apps Job | contenedor de un solo uso (§7) |
| Secretos | `secrets` de la Container App | archivo `.env` con permisos `600` / gestor de secretos del host |
| Identidad para el KEK | Managed identity + Key Vault | KEK `local` en el `.env`, o implementar `AwsKmsKeyProtector` (§3) |
| Backups de la BD | Los hace Neon | Tuyos (§10) |
| Rollback | revisiones de Container Apps | re-`docker compose up` con el tag anterior (§9) |

CI (`.github/workflows/ci.yml`) solo compila y prueba: **no hay pipeline de
despliegue que reescribir**. El despliegue es manual (o un script propio en el
VPS).

---

## Decisiones antes de empezar

1. **PostgreSQL: ¿gestionado o autoalojado?**
   - **Gestionado** (Neon sigue sirviendo, o Crunchy, RDS, Azure DB…): menos
     trabajo de ops, backups y PITR incluidos. Mantené `Database:Provider=neon`
     si es Neon; `postgres` para el resto.
   - **Autoalojado** (contenedor `postgres:16` en el mismo VPS, o un servicio del
     sistema): control total, cero costo extra, pero los backups, el `VACUUM`,
     los parches y el tuning son tuyos. Recomendado solo si ya administrás
     Postgres.
   - En ambos casos hace falta el rol restringido `novafe_app` (§2) — RLS es la
     red de seguridad de producción (ver [`multi-tenancy.md`](multi-tenancy.md)).

2. **Protección de la KEK.**
   - `local`: la master key (32 bytes base64) vive en el `.env` del VPS. Simple,
     y **aceptable para la certificación DGII** (Fase 1 del roadmap de
     [`certificates.md`](certificates.md)). Es un downgrade de postura de
     seguridad respecto a Key Vault: la clave está en disco, no en un HSM.
   - `aws-kms` / `gcp-kms`: no existen aún, pero el patrón está trazado — una
     clase `IKeyProtector` + un `case` en `KeyProtectorFactory`. ~1 archivo si
     querés mantener la KEK fuera del proceso.
   - Si ya hay certificados cargados envueltos con Azure Key Vault, hace falta el
     **paso de re-envoltura** (§8). En un despliegue nuevo, no.

3. **Escalado.** La API corre bien como **una sola instancia**. Se puede escalar
   horizontalmente (los workers usan `SKIP LOCKED`, el motor de settings hace
   poll de la BD), con dos matices: la caché en memoria se vuelve por-instancia
   (más llamadas de token a la DGII, no incorrectitud) y el
   `ExpiryMonitorWorker` corre en cada instancia (dedup en `expiry_notifications`
   lo cubre). Si vas a más de ~2 instancias, planificá Redis
   ([`redis.md`](redis.md)) y quizá separar los workers.

4. **Dominio y DNS.** Un `A`/`AAAA` al VPS. La API puede no ser pública (el
   dashboard le habla por su BFF) — si la exponés solo para el dashboard,
   restringí por firewall o red privada.

---

## Prerrequisitos

- Un VPS con Linux (Debian/Ubuntu LTS), **2 vCPU / 2 GiB RAM** de mínimo cómodo
  (1 GiB alcanza para la API sola; Postgres autoalojado en el mismo host pide
  más).
- `docker` y `docker compose` v2 instalados.
- Un usuario no-root con acceso a Docker.
- Un dominio apuntando al VPS (para TLS automático).
- Acceso al registro de imágenes (GitHub Container Registry, Docker Hub, o
  `docker build` en el propio VPS).
- El connection string del rol **dueño** de la base (para crear `novafe_app`).

---

## 1. Base de datos

### Opción A — Postgres autoalojado (contenedor)

Va incluido en el `docker-compose.prod.yml` de §5. Puntos a cuidar:

- **Volumen persistente** para `/var/lib/postgresql/data` (ya está en el compose).
- **No publicar el puerto 5432 al host** — que solo lo vea la red de Docker.
- TLS entre la API y Postgres en la misma máquina es opcional: poné
  `Database__RequireSsl=false` y `Database__Provider=postgres`. Si preferís TLS,
  configurá `ssl` en el `postgresql.conf` y dejá `RequireSsl=true`.

### Opción B — Postgres gestionado

Solo necesitás el connection string. Si es Neon, usá el **endpoint directo, no el
`-pooler`** (ver [`deployment.md`](deployment.md) §Neon — con transaction pooling,
RLS deja de aislar). La app avisa fuerte en el log si detecta un host `-pooler`.

---

## 2. Rol de aplicación restringido

Igual que en cualquier despliegue. Con el connection string del **dueño**:

```bash
# Editá deploy/sql/001-app-role.sql (contraseña + nombre de base) o pasá las vars:
psql "<connection string del dueño>" \
  -v app_password="'<contraseña fuerte para novafe_app>'" \
  -v dbname="'novafe'" \
  -f deploy/sql/001-app-role.sql
```

El script es idempotente. Volvé a correr los `GRANT … ON ALL …` después de cada
release que agregue tablas (o dejá que `ALTER DEFAULT PRIVILEGES` lo herede, que
ya está en el script). Cubierto por `RowLevelSecurityTests`.

El connection string que usará la **app** se arma con `novafe_app`, no el dueño.

---

## 3. La KEK

### `local` (recomendado para empezar)

```bash
openssl rand -base64 32
```

Ese valor va en `CertificateVault__MasterKey` (§4). Guardalo también fuera del
VPS (gestor de contraseñas): si se pierde, los certificados cargados quedan
irrecuperables.

### KMS de otro proveedor

No hay implementación todavía. El molde:

1. `src/Infrastructure/Security/AwsKmsKeyProtector.cs` (o Gcp) implementando
   `IKeyProtector` — mirá `AzureKeyVaultKeyProtector.cs` como referencia
   (envuelve/desenvuelve la DEK, la KEK nunca entra al proceso).
2. Un `case` en `KeyProtectorFactory.Create` y en `CertificateVaultOptionsValidator`.
3. Las constantes de proveedor en `CertificateVaultOptions`.

Ni el dominio ni la capa Application se enteran.

---

## 4. Variables de entorno

.NET mapea `Sección__Clave` → `Sección:Clave`. Van en un archivo `.env` con
permisos `600`, junto al `docker-compose.prod.yml`.

| Variable | Obligatoria | Ejemplo / notas |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | sí | `Production` |
| `ConnectionStrings__Default` | sí (secreto) | Rol `novafe_app`. Autoalojado: `Host=postgres;Port=5432;Database=novafe;Username=novafe_app;Password=***`. Neon: host **directo**. |
| `Database__Provider` | no | `postgres` (default) o `neon`. `neon` sube el connect timeout y verifica la cadena TLS. |
| `Database__RequireSsl` | no | `true` en producción. `false` solo si la BD está en el mismo host sin TLS. |
| `Database__MigrateOnStartup` | no | Dejar en `false` (default fuera de Development). Las migraciones son el paso §7. |
| `Database__MaxPoolSize` | no | `20` default. Subilo si corrés varias instancias contra un Postgres holgado. |
| `CertificateVault__Provider` | no | `local` (default) o `azure-key-vault`. |
| `CertificateVault__MasterKey` | con `local` | KEK base64 de 32 bytes (`openssl rand -base64 32`). |
| `CertificateVault__KeyVaultKeyUri` | con `azure-key-vault` | Solo si seguís usando Key Vault desde el VPS. |
| `Security__AdminApiKey` | sí (secreto) | Clave estática de operador (`X-Admin-Key`). Sin ella, los endpoints de operador rechazan todo. |
| `Security__InternalApiKey` | con dashboard | Secreto compartido con el BFF de `web/` (`INTERNAL_API_KEY` allá). Vacío = canal humano deshabilitado. Ver [`human-auth.md`](human-auth.md). |
| `Dgii__EcfBaseUrl` | no | `https://ecf.dgii.gov.do` (default). |
| `Dgii__FcBaseUrl` | no | `https://fc.dgii.gov.do` (default). |
| `Cors__AllowedOrigins__0` | no | Origen del dashboard, solo si el navegador llama a la API directo (con el BFF de mismo origen no hace falta). |
| `Observability__OtlpEndpoint` | no | Endpoint OTLP (Grafana Alloy, Tempo, un colector…). Vacío = no exporta. |
| `RUN_MIGRATIONS_AND_EXIT` | solo el contenedor de migraciones | `true` — aplica migraciones + seeds y termina. |

`ASPNETCORE_HTTP_PORTS=8080` ya viene en el Dockerfile; no hace falta declararlo.

---

## 5. `docker-compose.prod.yml`

Ejemplo con Postgres autoalojado y Caddy como reverse proxy. Ajustá imágenes y
dominio.

```yaml
# docker-compose.prod.yml — NovaFE en un VPS
services:
  api:
    image: ghcr.io/emmanueletm/novafe-api:${TAG:?set TAG}
    env_file: .env
    environment:
      # El contenedor está detrás de Caddy: no publicar el puerto al mundo.
      ASPNETCORE_HTTP_PORTS: "8080"
    depends_on:
      postgres:
        condition: service_healthy
    restart: unless-stopped
    # Solo la red interna llega a la API; Caddy la alcanza por nombre.
    expose:
      - "8080"
    volumes:
      - api-logs:/app/logs           # opcional: el sink de archivo de Serilog
    healthcheck:
      test: ["CMD", "curl", "-fsS", "http://localhost:8080/health/live"]
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 20s

  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: novafe
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?set POSTGRES_PASSWORD}
    volumes:
      - pgdata:/var/lib/postgresql/data
    # Sin `ports:` — solo accesible desde la red de Docker.
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d novafe"]
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 15s

  caddy:
    image: caddy:2
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy-data:/data
      - caddy-config:/config
    depends_on:
      - api

volumes:
  pgdata:
  api-logs:
  caddy-data:
  caddy-config:
```

> Si usás Postgres gestionado, borrá el servicio `postgres`, el `depends_on` y el
> volumen `pgdata`; poné el connection string gestionado en `.env`.

---

## 6. Reverse proxy y TLS

`Caddyfile` (TLS automático por Let's Encrypt, redirección HTTP→HTTPS incluida):

```
api.novafe.example {
    reverse_proxy api:8080

    # Caddy ya envía X-Forwarded-For / X-Forwarded-Proto. La app los consume
    # (UseForwardedHeaders en Program.cs) y con eso Request.Scheme y la IP del
    # cliente son correctas para el rate limiter y los logs.
    encode gzip
}
```

Notas:

- La app hace `UseHttpsRedirection` internamente; como Caddy ya fuerza HTTPS y
  reenvía `X-Forwarded-Proto: https`, no hay loop.
- `Program.cs` limpia `KnownProxies`/`KnownIPNetworks` (acepta los headers
  reenviados de cualquier salto). Eso es seguro **solo** porque el puerto 8080 no
  está expuesto: nadie puede saltarse Caddy y spoofear `X-Forwarded-For`.
  Mantené el `expose:` en vez de `ports:` para la API.
- nginx o Traefik sirven igual; el requisito es que reenvíen `X-Forwarded-*`.

---

## 7. Migraciones (cada release, antes de arrancar la versión nueva)

Un contenedor de un solo uso con la misma imagen:

```bash
docker run --rm \
  --network novafe_default \
  --env-file .env \
  -e RUN_MIGRATIONS_AND_EXIT=true \
  ghcr.io/emmanueletm/novafe-api:${TAG}
```

Aplica migraciones + seeds y sale con código 0. El advisory lock de
`DatabaseInitializer` hace segura la ejecución aunque coincida con otra. Si la
migración nueva no es compatible hacia atrás con la imagen vieja que sigue
corriendo, hay una ventana entre este paso y el `up` de §8 — a la escala de
arranque se acepta; si molesta, expand/contract (migración aditiva, deploy,
migración destructiva después).

> El connection string de migraciones puede ser el del **dueño** (para que
> `ALTER DEFAULT PRIVILEGES` funcione) y el de runtime el de `novafe_app`. Si
> usás el mismo rol para ambos, dale al rol de migraciones permiso de DDL.

---

## 8. Primer arranque y verificación

```bash
# 1. Subí el .env, el docker-compose.prod.yml y el Caddyfile al VPS.
# 2. Base de datos (una vez): §1 opción A levanta el contenedor; luego §2.
docker compose -f docker-compose.prod.yml up -d postgres
psql "postgres://postgres:***@localhost:5432/novafe" -f deploy/sql/001-app-role.sql   # si publicás 5432 temporalmente, o `docker compose exec`

# 3. Migraciones
docker run --rm --network novafe_default --env-file .env \
  -e RUN_MIGRATIONS_AND_EXIT=true ghcr.io/emmanueletm/novafe-api:${TAG}

# 4. Arrancá todo
docker compose -f docker-compose.prod.yml up -d

# 5. Verificá
curl -fsS https://api.novafe.example/health/ready     # 200 + {"status":"Healthy"}
docker compose -f docker-compose.prod.yml logs -f api  # sin warning de pooler, sin warning de AdminApiKey
```

Chequeos de humo:

- `GET /health/live` → 200 (proceso vivo).
- `GET /health/ready` → 200 (Postgres alcanzable).
- El log **no** muestra "El host de la base parece un pooler en modo transaction".
- El log **no** muestra "Security:AdminApiKey no está configurada".
- `POST /api/v1/dev/sandbox` **no** debe existir (solo Development) — si responde,
  `ASPNETCORE_ENVIRONMENT` está mal.
- Registrar un tenant de prueba con `X-Admin-Key` y emitir un e-CF contra el
  simulador de la DGII (ver [`local-e2e.md`](local-e2e.md)).

### Re-envoltura de la KEK (solo si venís de Key Vault con certificados cargados)

```bash
docker run --rm --network novafe_default --env-file .env \
  -e CertificateVault__Provider=local \
  -e CertificateVault__MasterKey="<KEK nueva base64>" \
  -e CertificateVault__Rewrap__Enabled=true \
  -e CertificateVault__Rewrap__FromProvider=azure-key-vault \
  -e CertificateVault__Rewrap__FromKeyVaultKeyUri="https://<vault>.vault.azure.net/keys/cert-kek" \
  ghcr.io/emmanueletm/novafe-api:${TAG}
```

Itera tenant por tenant (todo-o-nada por tenant), re-envuelve la DEK con la KEK
nueva y sale. Después quitá la sección `Rewrap` y arrancá normal. Detalle en
[`certificates.md`](certificates.md) §"Cambiar de proveedor de KEK".

---

## 9. Flujo de cada release

```bash
# a) build + push (o build en el VPS)
docker build -f src/Service/Dockerfile -t ghcr.io/emmanueletm/novafe-api:<tag> .
docker push ghcr.io/emmanueletm/novafe-api:<tag>

# b) en el VPS: actualizá TAG en .env, corré migraciones
export TAG=<tag>
docker run --rm --network novafe_default --env-file .env \
  -e RUN_MIGRATIONS_AND_EXIT=true ghcr.io/emmanueletm/novafe-api:$TAG

# c) recreá el contenedor de la API con la imagen nueva
docker compose -f docker-compose.prod.yml up -d api

# d) verificá
curl -fsS https://api.novafe.example/health/ready
```

`docker compose up -d api` hace un recreate: hay unos segundos de corte (una sola
instancia). Para cero downtime necesitás dos réplicas detrás de Caddy y recrearlas
de a una — o aceptar la ventana, que a esta escala es lo razonable.

El worker de envío a la DGII recibe `SIGTERM` en el recreate y tiene 25 s para
terminar el tick en curso (`HostOptions.ShutdownTimeout` en `Program.cs`); lo que
no alcance queda en el outbox y lo retoma la instancia nueva.

## 10. Rollback

```bash
# Volvé al tag anterior
export TAG=<tag-anterior>
docker compose -f docker-compose.prod.yml up -d api
```

Una migración ya aplicada **no** se revierte sola — hay que hacerlo a mano contra
la base (no hay down-migrations en el flujo). Por eso conviene que cada migración
sea compatible hacia atrás al menos un release.

## 11. Backups

Responsabilidad tuya con Postgres autoalojado:

- `pg_dump` diario a almacenamiento fuera del VPS (S3, Backblaze, otro host):
  ```bash
  docker compose -f docker-compose.prod.yml exec -T postgres \
    pg_dump -U postgres -Fc novafe > novafe-$(date +%F).dump
  ```
- Para PITR real, `pgBackRest` o `wal-g` con archivado de WAL.
- Probá una restauración cada tanto — un backup no verificado no es un backup.
- Qué proteger sí o sí: la tabla `certificate_secrets` (certificados de los
  clientes, cifrados) y **la KEK** (que está fuera de la BD, en el `.env`).

Con Postgres gestionado esto lo hace el proveedor; verificá la política de
retención.

## 12. Observabilidad

- **Logs**: Serilog escribe a `stdout` (lo captura `docker compose logs`) y a
  `/app/logs` dentro del contenedor (montá el volumen `api-logs` si querés
  persistirlo). Para centralizar: un colector (Grafana Alloy, Vector, Promtail)
  leyendo el socket de Docker.
- **Trazas y métricas**: poné `Observability__OtlpEndpoint` apuntando a un
  colector OTLP (Grafana Tempo/Mimir, un OpenTelemetry Collector, Uptrace…). Sin
  endpoint no se exporta nada y no pasa nada.
- **Disponibilidad**: un monitor externo (UptimeRobot, Better Stack) contra
  `https://api.novafe.example/health/ready`.

---

## Dashboard (`web/`)

Vive en Vercel y también está desacoplado a propósito:

- **Base de datos de Better Auth**: `lib/db.ts` es driver-por-contrato.
  `DATABASE_DRIVER=pg` (node-postgres, portable a cualquier Postgres) o `neon`.
  Mudarse de Neon = `DATABASE_DRIVER=pg` + `DATABASE_URL` al Postgres nuevo. Las
  tablas de auth viven en el schema `auth` (Drizzle), aparte del `public` de la
  API.
- **Correo**: `lib/email.ts` por contrato — Resend, o log a consola. Swappable a
  otro proveedor.
- **Hosting**: es un Next.js 16 estándar. Corre en cualquier Node 20+ o en un
  contenedor (`next build && next start`). Para sacarlo de Vercel:
  contenedorizarlo, ponerlo detrás del mismo Caddy (otro bloque de sitio), y
  setear `APP_API_URL` al servicio `api` interno.
- **Variables**: ver `web/CLAUDE.md` §Env (`DATABASE_URL`, `BETTER_AUTH_SECRET`,
  `BETTER_AUTH_URL`, `INTERNAL_API_KEY`, `GITHUB_*`, …).

Ver [`human-auth.md`](human-auth.md) y `web/CLAUDE.md` para el detalle del canal
humano y el BFF.

---

## Checklist

- [ ] VPS con Docker + Compose v2, usuario no-root, firewall (solo 80/443
      públicos).
- [ ] Dominio con DNS al VPS.
- [ ] Postgres elegido (autoalojado o gestionado); si es Neon, endpoint
      **directo**.
- [ ] Rol `novafe_app` creado (`deploy/sql/001-app-role.sql`).
- [ ] KEK generada (`openssl rand -base64 32`) y respaldada fuera del VPS.
- [ ] `.env` completo, permisos `600`.
- [ ] `docker-compose.prod.yml` + `Caddyfile` en el VPS.
- [ ] Migraciones corridas (`RUN_MIGRATIONS_AND_EXIT=true`).
- [ ] `docker compose up -d`; `/health/ready` = 200.
- [ ] Logs sin warning de pooler ni de AdminApiKey.
- [ ] Smoke test: registrar tenant + emitir e-CF contra el simulador.
- [ ] Re-envoltura de KEK corrida (solo si migrás desde Key Vault con
      certificados).
- [ ] Backups automatizados y una restauración probada.
- [ ] Monitor externo contra `/health/ready`.
- [ ] Dashboard: `DATABASE_DRIVER` / `APP_API_URL` apuntando al stack nuevo.

## Lo que queda peor que en Azure

- **La KEK en disco** (con `local`) en lugar de un HSM. Mitigable implementando
  un `IKeyProtector` de KMS.
- **Backups, parches del SO, `VACUUM`, tuning de Postgres** — todo manual si
  autoalojás la BD.
- **Sin rolling update sin downtime** de fábrica: una instancia = ventana de
  segundos en cada release.
- **Sin autoescalado**: la capacidad la fijás vos con el tamaño del VPS.
- **TLS, DDoS, `fail2ban`, hardening SSH** — responsabilidades nuevas.

A cambio: costo fijo y predecible, sin lock-in, y un modelo mental más simple
(`docker compose` en un servidor).
