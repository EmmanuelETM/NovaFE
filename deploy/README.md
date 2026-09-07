# deploy/ — infraestructura y runbook

Despliegue de NovaFE en **Azure Container Apps** + **Azure Key Vault**, contra
**Neon** (PostgreSQL). El "por qué" y los puntos de swap están en
[`docs/deployment.md`](../docs/deployment.md); esto es el "cómo".

```
deploy/
  main.bicep            infraestructura Azure (Container Apps, Key Vault, identidad, Job de migraciones)
  main.parameters.json  plantilla de parámetros (sin secretos reales)
  sql/001-app-role.sql  rol runtime restringido para que RLS aísle de verdad
```

## Primer despliegue: por etapas

Dos cosas solo se prueban con las cuentas reales delante: `AzureKeyVaultKeyProtector`
(nunca corrió contra un Key Vault real) y el propio Bicep (validarlo con
`az bicep build` antes que nada). Conviene ir por etapas para aislar las variables.

- **Etapa 0 — contenedor local contra Neon, sin Azure (gratis).**
  Crear el proyecto en Neon; correr `sql/001-app-role.sql` como el rol dueño para
  crear `novafe_app`; correr la imagen local apuntando al endpoint **directo** de
  Neon (no `-pooler`) como `novafe_app`, con `Database__Provider=neon`. Confirmar:
  conecta con TLS `VerifyFull`, el aviso de pooler NO aparece,
  `RUN_MIGRATIONS_AND_EXIT=true` aplica las migraciones, `GET /health/ready` = 200,
  se puede registrar un tenant. Esto de-risquea toda la mitad de la base de datos
  sin gastar nada.

- **Etapa 1 — Azure con KEK `local`.**
  Desplegar el Bicep pero con `-p ...` que fuerce `CertificateVault__Provider=local`
  y un `MasterKey` como secreto (editar el `env` del Bicep o pasarlo como override).
  Prueba ACA (ingress, forwarded headers, el contenedor, Neon desde Azure, el
  worker del outbox, el Job de migraciones) sin la variable de Key Vault.

- **Etapa 2 — pasar a Key Vault.**
  `az deployment group create` otra vez con los parámetros de KV (el Bicep ya crea
  vault + clave + role assignment). Si la Etapa 1 cargó certificados de prueba,
  correr el Job de re-envoltura (`CertificateVault:Rewrap`, ver
  [`../docs/certificates.md`](../docs/certificates.md)). Confirmar que la firma
  sigue funcionando.

## Requisitos

- `az` CLI (`az login`, `az account set -s <subscription>`)
- Un resource group: `az group create -n novafe-prod-rg -l eastus`
- Un Azure Container Registry (o una imagen pública)
- Una base en Neon con el endpoint **directo** (no `-pooler`)

## 1. Imagen

```bash
az acr build -r novafeprod -t novafe-api:$(git rev-parse --short HEAD) -f src/Service/Dockerfile .
```

## 2. Base de datos (una vez)

Con el connection string del rol **dueño** de Neon:

```bash
export NOVAFE_APP_PASSWORD='<contraseña fuerte para novafe_app>'
export NOVAFE_DB_NAME='novafe'
psql "<connection string del dueño>" -f deploy/sql/001-app-role.sql
```

El connection string que usará la app se arma con `novafe_app` (no el dueño) y el
host **directo**:

```
Host=ep-xxx.us-east-2.aws.neon.tech;Database=novafe;Username=novafe_app;Password=***;SSL Mode=VerifyFull
```

## 3. Infraestructura

Editá `main.parameters.json` (o pasá los valores por línea de comandos). **Los
secretos NO van en el archivo** — pasalos con `-p`:

```bash
az deployment group create \
  -g novafe-prod-rg \
  -f deploy/main.bicep \
  -p @deploy/main.parameters.json \
  -p containerImage=novafeprod.azurecr.io/novafe-api:<tag> \
  -p dbConnectionString="$DB_CONNECTION_STRING" \
  -p adminApiKey="$ADMIN_API_KEY"
```

Salidas útiles: `apiUrl`, `kekKeyUri`, `migrationsJobName`.

Validar la plantilla sin desplegar:

```bash
az bicep build --file deploy/main.bicep
az deployment group what-if -g novafe-prod-rg -f deploy/main.bicep -p @deploy/main.parameters.json
```

## 4. Cada release

```bash
# a) build + push
az acr build -r novafeprod -t novafe-api:<tag> -f src/Service/Dockerfile .

# b) migraciones (esperar a que termine OK)
az containerapp job start -n novafe-prod-migrations -g novafe-prod-rg
az containerapp job execution list -n novafe-prod-migrations -g novafe-prod-rg -o table

# c) nueva imagen en la API
az containerapp update -n novafe-prod-api -g novafe-prod-rg \
  --image novafeprod.azurecr.io/novafe-api:<tag>

# d) verificar
curl -fsS https://<apiFqdn>/health/ready
```

## 5. Rollback

```bash
az containerapp revision list -n novafe-prod-api -g novafe-prod-rg -o table
az containerapp revision activate -n novafe-prod-api -g novafe-prod-rg --revision <anterior>
```

Una migración ya aplicada no se revierte sola: hay que hacerlo a mano contra la
base.

## Notas

- **Managed identity**: la Container App y el Job usan la misma identidad
  user-assigned. Tiene *Key Vault Crypto User* sobre el vault y *AcrPull* sobre el
  registry (si se pasó `acrName`). El `AZURE_CLIENT_ID` lo inyecta el Bicep para
  que `DefaultAzureCredential` no dude entre identidades.
- **Purge protection** del Key Vault: `true` por defecto. En un entorno de pruebas
  que vayas a borrar y recrear, pasá `-p keyVaultPurgeProtection=false`.
- **Secretos**: el Bicep los declara como `secrets` de la Container App a partir
  de los parámetros `@secure()`. Para rotarlos, `az containerapp secret set` +
  reiniciar la revisión, o re-desplegar.
