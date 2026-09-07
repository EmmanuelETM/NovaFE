// ============================================================================
//  NovaFE — infraestructura de despliegue en Azure Container Apps
// ============================================================================
//  Crea: Log Analytics, un Container Apps Environment, un Key Vault con la clave
//  RSA que protege la KEK del vault de certificados (IKeyProtector =
//  azure-key-vault), una identidad administrada para la app, la Container App de
//  la API y un Container Apps Job que corre las migraciones antes de cada
//  release.
//
//  La base de datos NO se crea acá: es Neon (gestionada, fuera de Azure). Su
//  connection string entra como secreto (parámetro `dbConnectionString`). Ver
//  docs/deployment.md.
//
//  Desplegar:
//    az deployment group create -g <rg> -f deploy/main.bicep -p @deploy/main.parameters.json
// ============================================================================

targetScope = 'resourceGroup'

@description('Prefijo para el nombre de los recursos, p. ej. "novafe" o "novafe-prod".')
@minLength(3)
@maxLength(20)
param namePrefix string

@description('Región. Por defecto la del resource group.')
param location string = resourceGroup().location

@description('Imagen del contenedor, completa: <registry>/<repo>:<tag>.')
param containerImage string

@description('Nombre del Azure Container Registry (mismo resource group). Vacío = imagen pública, sin auth.')
param acrName string = ''

@description('Connection string de Neon (endpoint DIRECTO, no -pooler). Se guarda como secreto.')
@secure()
param dbConnectionString string

@description('Clave estática de operador (header X-Admin-Key). Se guarda como secreto.')
@secure()
param adminApiKey string

@description('URL base de la DGII para e-CF.')
param dgiiEcfBaseUrl string = 'https://ecf.dgii.gov.do'

@description('URL base de la DGII para Facturas de Consumo (RFCE).')
param dgiiFcBaseUrl string = 'https://fc.dgii.gov.do'

@description('Origen del dashboard permitido por CORS (vacío = ninguno).')
param dashboardOrigin string = ''

@description('Endpoint OTLP para trazas/métricas (vacío = no se exporta).')
param otlpEndpoint string = ''

@description('CPU por réplica (vCPU).')
param containerCpu string = '0.5'

@description('Memoria por réplica.')
param containerMemory string = '1Gi'

@description('Réplicas mínimas. 1 mantiene vivo el worker del outbox (EcfSubmissionWorker).')
@minValue(1)
param minReplicas int = 1

@description('Réplicas máximas.')
@minValue(1)
param maxReplicas int = 3

@description('Protección contra purga del Key Vault. true en producción; false facilita borrar y recrear en pruebas.')
param keyVaultPurgeProtection bool = true

// ---------------------------------------------------------------------------
//  Nombres derivados
// ---------------------------------------------------------------------------
var logAnalyticsName = '${namePrefix}-logs'
var environmentName = '${namePrefix}-cae'
var identityName = '${namePrefix}-api-id'
var keyVaultName = take('${replace(namePrefix, '-', '')}kv${uniqueString(resourceGroup().id)}', 24)
var kekKeyName = 'cert-kek'
var apiAppName = '${namePrefix}-api'
var migrationsJobName = '${namePrefix}-migrations'

// Roles integrados
var keyVaultCryptoUserRoleId = '12338af0-0e69-4776-bea7-57ae8d297424'
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

// ---------------------------------------------------------------------------
//  Observabilidad
// ---------------------------------------------------------------------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ---------------------------------------------------------------------------
//  Container Apps Environment
// ---------------------------------------------------------------------------
resource containerEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ---------------------------------------------------------------------------
//  Identidad administrada de la API
// ---------------------------------------------------------------------------
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

// ---------------------------------------------------------------------------
//  Key Vault + clave RSA para la KEK
// ---------------------------------------------------------------------------
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: keyVaultPurgeProtection ? true : null
  }
}

resource kekKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: keyVault
  name: kekKeyName
  properties: {
    kty: 'RSA'
    keySize: 3072
    keyOps: [ 'wrapKey', 'unwrapKey' ]
  }
}

// La identidad de la app puede envolver/desenvolver con la clave (no leerla).
resource kvCryptoUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, identity.id, keyVaultCryptoUserRoleId)
  scope: keyVault
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCryptoUserRoleId)
  }
}

// ---------------------------------------------------------------------------
//  Pull desde ACR con la identidad (si se pasó acrName)
// ---------------------------------------------------------------------------
resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = if (!empty(acrName)) {
  name: acrName
}

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(acrName)) {
  name: guid(resourceGroup().id, identity.id, acrPullRoleId, acrName)
  scope: acr
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
  }
}

var registries = empty(acrName) ? [] : [
  {
    server: acr.properties.loginServer
    identity: identity.id
  }
]

// ---------------------------------------------------------------------------
//  Variables de entorno comunes (API + Job de migraciones)
// ---------------------------------------------------------------------------
var baseEnv = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
  { name: 'ConnectionStrings__Default', secretRef: 'db-connection-string' }
]

var apiEnv = concat(baseEnv, [
  { name: 'Database__Provider', value: 'neon' }
  { name: 'Database__MigrateOnStartup', value: 'false' }
  { name: 'CertificateVault__Provider', value: 'azure-key-vault' }
  { name: 'CertificateVault__KeyVaultKeyUri', value: '${keyVault.properties.vaultUri}keys/${kekKeyName}' }
  { name: 'Security__AdminApiKey', secretRef: 'admin-api-key' }
  { name: 'Dgii__EcfBaseUrl', value: dgiiEcfBaseUrl }
  { name: 'Dgii__FcBaseUrl', value: dgiiFcBaseUrl }
  { name: 'AZURE_CLIENT_ID', value: identity.properties.clientId }
], empty(dashboardOrigin) ? [] : [
  { name: 'Cors__AllowedOrigins__0', value: dashboardOrigin }
], empty(otlpEndpoint) ? [] : [
  { name: 'Observability__OtlpEndpoint', value: otlpEndpoint }
])

var commonSecrets = [
  { name: 'db-connection-string', value: dbConnectionString }
  { name: 'admin-api-key', value: adminApiKey }
]

// ---------------------------------------------------------------------------
//  Container App — la API
// ---------------------------------------------------------------------------
resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    managedEnvironmentId: containerEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: registries
      secrets: commonSecrets
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: 'api'
          image: containerImage
          resources: {
            cpu: json(containerCpu)
            memory: containerMemory
          }
          env: apiEnv
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health/live', port: 8080 }
              initialDelaySeconds: 10
              periodSeconds: 15
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health/ready', port: 8080 }
              initialDelaySeconds: 5
              periodSeconds: 10
              failureThreshold: 3
            }
            {
              type: 'Startup'
              httpGet: { path: '/health/ready', port: 8080 }
              initialDelaySeconds: 5
              periodSeconds: 5
              failureThreshold: 30
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http'
            http: { metadata: { concurrentRequests: '80' } }
          }
        ]
      }
    }
  }
}

// ---------------------------------------------------------------------------
//  Container Apps Job — migraciones (paso previo a cada release)
// ---------------------------------------------------------------------------
resource migrationsJob 'Microsoft.App/jobs@2024-03-01' = {
  name: migrationsJobName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    environmentId: containerEnv.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 600
      replicaRetryLimit: 1
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: registries
      secrets: commonSecrets
    }
    template: {
      containers: [
        {
          name: 'migrations'
          image: containerImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: concat(baseEnv, [
            { name: 'RUN_MIGRATIONS_AND_EXIT', value: 'true' }
          ])
        }
      ]
    }
  }
}

// ---------------------------------------------------------------------------
//  Salidas
// ---------------------------------------------------------------------------
output apiFqdn string = apiApp.properties.configuration.ingress.fqdn
output apiUrl string = 'https://${apiApp.properties.configuration.ingress.fqdn}'
output keyVaultName string = keyVault.name
output kekKeyUri string = '${keyVault.properties.vaultUri}keys/${kekKeyName}'
output identityPrincipalId string = identity.properties.principalId
output migrationsJobName string = migrationsJob.name
