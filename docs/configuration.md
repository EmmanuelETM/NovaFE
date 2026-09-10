# Configuración y settings

> **Documento de diseño / propuesta.** Nada de esto está construido todavía. Fija
> el modelo antes de escribir la primera tabla, para que la config dinámica y los
> settings de cliente no crezcan ad-hoc por los módulos.

Un servicio de cumplimiento fiscal tiene que cambiar comportamiento **sin
desplegar**: pausar un worker en un incidente, activar el modo contingencia
cuando la DGII se cae, ajustar un límite de plan negociado, dejar que el
contribuyente elija el layout de su Representación Impresa. Hoy todo eso exige una
revisión nueva de Container Apps.

Este diseño combina tres iteraciones previas (la propuesta original de este doc,
el modelo `ISystemOptions`/tabla `AppSettings` de ECFGateway, y el modelo de
definiciones tipadas de Economato) con las prácticas de Vercel, Netflix y los
proveedores de feature-flags.

---

## Principios

Destilados de cómo lo resuelven equipos más grandes (ver [Referencias](#referencias)):

1. **Dos caminos separados.** Config *versionada con el código* vs. config
   *operativa*. SLAs, almacenes y permisos distintos. (Vercel: env vars vs. Edge Config.)
2. **Esquema en código.** Cada setting se declara una vez, tipado, con un default
   **siempre válido**. La BD solo guarda sobrescrituras. (Configerator, Archaius.)
3. **Validar en la escritura, no en la lectura.** Un valor inválido se rechaza en
   el `PUT`; si aun así una fila queda corrupta, la lectura cae al default, nunca lanza.
4. **Resolución en capas con precedencia explícita.** `default → plataforma → plan
   → tenant`. Un solo algoritmo, no tres subsistemas. (LaunchDarkly / OpenFeature.)
5. **Lectura sin I/O.** Snapshot en memoria; la propagación es un poll barato de
   un contador de versión. El hot path nunca toca la BD. (Vercel Edge Config.)
6. **Degradación segura.** Si el almacén no responde en una recarga, se mantiene
   el último valor bueno conocido; nunca un salto silencioso al default por un
   fallo transitorio. (Archaius persisted fallback.)
7. **Kill-switches en carril aparte**, con latencia de segundos, no de minutos.
8. **Todo cambio auditado e inmutable.** Una ráfaga de cambios alerta.
9. **Observabilidad.** La versión de config efectiva es visible por instancia; el
   drift entre réplicas se detecta y alarma.
10. **Aburrido.** Un almacén, un caché, un bus. Cada mecanismo extra es un modo de
    fallo más.

---

## Dos sistemas

### 1. Bootstrap — versionado con el código

`appsettings.json` + `Section__Key` en Container Apps (`env` / `secrets` +
`secretRef`) → `IOptions<T>` con `ValidateOnStart`. Cambiar = nueva revisión =
restart rolling.

**Vive aquí** todo lo necesario *antes* de que la app sirva tráfico o llegue a la
BD, y todo lo security-sensitive:

- Secretos: connection strings, `Security:AdminApiKey`, `Security:InternalApiKey`,
  KEK / `CertificateVault:*`.
- Selección de proveedor: `CertificateVault:Provider`, `Database:Provider`.
- Parámetros cripto de firma XMLDSig (fijos, afirmados en pruebas).
- Orígenes CORS, Serilog, endpoints OTLP.

Ver [`deployment.md`](deployment.md). Esta capa **no cambia** con este diseño.

### 2. Runtime — config operativa

Definiciones en código + tabla de sobrescrituras en PostgreSQL, expuesta por
`ISettingsReader`. Cambia **sin desplegar**. Todo lo demás vive aquí.

### Regla de decisión

| Pregunta | Sí → |
|---|---|
| ¿Se necesita antes de aceptar tráfico, o es un secreto? | Bootstrap |
| ¿El valor varía de fila a fila en un catálogo (precio de un ítem, `ClicksPerSheet`)? | **Ni uno ni otro** — es una columna de su módulo |
| ¿Es un parámetro de negocio/operación que un admin ajustaría? | Runtime |

---

## Modelo runtime: definiciones en código

### `SettingDefinition<T>`

Clase base abstracta en `src/Domain/Settings/`. Subtipos concretos —
`TextSetting`, `IntegerSetting`, `DecimalSetting`, `BooleanSetting`,
`OptionSetting<TEnum>`, `DurationSetting` — cada uno sabe **validar, parsear y
formatear** su propio valor. Un valor corrupto en la fila cae al `Default` en vez
de lanzar.

Cada definición declara:

| Campo | Para qué |
|---|---|
| `Key` | namespaced: `submission.worker.poll_interval`, `representation.default_layout` |
| `Group`, `Label`, `Description`, `Unit?` | la pantalla de administración |
| `Scope` | `Platform` · `Plan` · `Tenant` (ver [Resolución](#resolución-en-capas)) |
| `Default` | valor de código; **siempre válido** (una prueba lo verifica) |
| `TenantWritable` | scope `Tenant`: ¿lo edita el contribuyente, o solo el operador por-tenant? |
| `Sensitive` | el `PUT` exige confirmación explícita (`?confirm=true`) |
| `Deprecated` | oculto en la UI; sigue resolviendo; se loguea si aún está sobrescrito |
| validación por tipo | rango, opciones, regex, longitud |

### Registro por reflexión

`SettingDefinitions.All` se arma por reflexión sobre los campos estáticos de la
clase, igual que los smart-enums del dominio. **Declarar el campo es todo lo
necesario** para que el setting exista, aparezca en `/settings` y funcione con su
default desde el deploy. Sin migración por setting, **sin seeder**.

```csharp
public static class SettingDefinitions
{
    public static class Submission
    {
        public static readonly DurationSetting PollInterval = new(
            key: "submission.worker.poll_interval",
            group: "Envío a la DGII",
            label: "Intervalo del worker",
            @default: TimeSpan.FromSeconds(10),
            min: TimeSpan.FromSeconds(1), max: TimeSpan.FromMinutes(5),
            scope: SettingScope.Platform);

        public static readonly BooleanSetting FastPathEnabled = new(
            "submission.fast_path.enabled", "Envío a la DGII",
            "Fast-path inline en POST /ecf", @default: true,
            scope: SettingScope.Platform, killSwitch: true);
    }
}
```

### Ciclo de vida

- **Añadir**: declarar el campo. Funciona con su default de inmediato.
- **Deprecar**: `Deprecated = true`. La pantalla lo esconde; sigue resolviendo.
- **Eliminar**: borrar el campo. Las filas huérfanas se ignoran en la resolución;
  un job periódico las purga (o se dejan — son diminutas).

---

## Resolución en capas

El **valor efectivo** de un setting para un contexto `(tenant, environment)` se
resuelve por precedencia. No todos los settings pasan por todas las capas — el
`Scope` de la definición dice cuáles aplican.

| # | Capa | Origen | Aplica a scope |
|---|---|---|---|
| 1 | Default de código | `SettingDefinition.Default` | todos (piso) |
| 2 | Override de plataforma | fila en `platform_settings` | Platform, Plan, Tenant |
| 3 | Valor de plan | tabla `plans` ([`pricing.md`](pricing.md)) | Plan |
| 4 | Override de suscripción | `tenant_subscriptions` (deals a medida) | Plan |
| 5 | Setting de tenant | fila en `tenant_settings` | Tenant |

Gana la capa más alta que tenga valor. Esto es donde el `override ?? plan` de
[`pricing.md`](pricing.md) se implementa de verdad — la app **nunca** ramifica por
el nombre del plan.

**Dimensión de ambiente** (test / cert / prod): la fila de override lleva una
columna `environment` nullable. La resolución prefiere la fila que coincide con el
ambiente del contexto; si no hay, usa la fila `environment = null`. La mayoría de
los settings se declaran sin distinción de ambiente; la columna existe para el día
que uno la necesite (p. ej. timeouts distintos contra TestECF).

---

## Lectura

### `ISettingsReader` (Application)

```csharp
Task<T> GetAsync<T>(SettingDefinition<T> def, CancellationToken ct);              // Platform
Task<T> GetAsync<T>(SettingDefinition<T> def, TenantId tenant, CancellationToken ct); // Plan / Tenant
```

Tipado, sin strings mágicos. `CachedSettingsReader` (Infrastructure) lo respalda
con el snapshot en memoria — sin I/O en el acceso normal. El `TenantId` sale de
`ICurrentTenant`; el worker lo fija por fila (igual que hoy).

### Lectura para pantalla

`ListSettingsUseCase` recorre las **definiciones** de `SettingDefinitions.All`
(no las filas), y por cada una devuelve: metadata (`group`, `label`,
`description`, `unit`, tipo, validación), valor efectivo, `resolvedFrom` y si está
sobrescrita. Así un setting recién declarado aparece con su default aunque nunca
se haya tocado, y una fila huérfana de una definición borrada se ignora. Es lo que
consume la pantalla del dashboard — por eso la metadata de presentación va en la
definición desde el paso 2 del [orden de trabajo](#orden-de-trabajo), no como un
añadido posterior.

### Fachadas tipadas por módulo

Para el config de plataforma que consumen workers y casos de uso hondos, no se
esparce `reader.GetAsync(SomeDefinition)` por el código. Un
`IConfigureOptions<SubmissionRuntimeOptions>` parte de los defaults y los
sobreescribe desde `ISettingsReader`; se expone como
`IOptionsMonitor<SubmissionRuntimeOptions>` y el call site lee `.CurrentValue`
síncrono. **Una fachada por módulo** (`SubmissionRuntimeOptions`,
`WebhookRuntimeOptions`, …), nunca un objeto de 50 propiedades.

`IOptionsMonitor` cachea el objeto calculado hasta que
`ISettingsCacheInvalidator` lo invalida → el próximo acceso recalcula el
`Configure`. Es el mecanismo de ECFGateway, por la vía limpia de .NET.

---

## Almacenamiento

Dos tablas con la misma forma; la separación es por aislamiento.

| Tabla | RLS | Filas | Análoga a |
|---|---|---|---|
| `platform_settings` | **no** (tabla de sistema) | override global, scope Platform/Plan/Tenant capa 2 | `audit_log`, `api_keys` |
| `tenant_settings` | **sí** (`ITenantOwned`) | override por contribuyente, scope Tenant capa 5 | `webhook_endpoints` |

```
key            text        -- FK lógica a una SettingDefinition
environment    text  null  -- test | cert | prod | null (agnóstico)
value          text        -- texto plano; lo parsea la definición
tenant_id      uuid        -- solo en tenant_settings (ITenantOwned)
updated_at / updated_by     -- interceptores EF
```

Volver al default **borra la fila físicamente** — no es `ISoftDeletable`.

### Bitácora de cambios

`platform_setting_changes` / `tenant_setting_changes`, append-only (solo `INSERT`,
como `audit_log` — esa ausencia de `UPDATE`/`DELETE` es la inmutabilidad):

```
key, environment, previous_value, new_value, changed_at, changed_by
```

`previous_value = null` distingue la **primera** sobrescritura; `new_value = null`
es la **vuelta al default**. La lectura para pantalla resuelve el **nombre** del
autor (no solo el id) con el join sobre `COALESCE(updated_by, created_by)` —
regla de idioma/salida.

### Contador de generación

Una fila: `settings_generation (value bigint)`. **Cualquier** escritura la
incrementa en la misma transacción. Es el pivote de la propagación.

---

## Despliegue y propagación

### Topología: `minReplicas = 1`

La API corre con **una sola réplica** (`minReplicas = 1`, `maxReplicas = 3–5` con
autoescalado por carga). Pre-cliente se está siempre en 1 — costo ~20–35 USD/mes.

No se escala a **cero** porque los workers (`EcfSubmissionWorker`,
`WebhookDeliveryWorker`, `ExpiryMonitorWorker`) viven en el mismo proceso: sin
réplica no se drena el outbox. El dev/test sí puede ir a cero.

Con 1 réplica hay un parpadeo posible en despliegues y en mantenimiento de
plataforma. Sin cliente, no importa. Cuando haya ingresos que lo paguen se sube
`minReplicas`, y **el diseño de abajo ya lo soporta sin cambios**.

### Sin Redis — no nos para

PostgreSQL es un almacén de config perfectamente bueno a esta escala. El hot path
de lectura es en memoria, así que la latencia de lectura es cero — la misma
propuesta de valor que Vercel Edge Config, sin infra nueva.

Tres piezas:

### 1. Snapshot en memoria

`SettingsCache` — singleton. Tiene el snapshot resuelto de las definiciones
Platform/Plan y, lazy, los overrides por tenant. **Toda lectura sale de aquí**; la
BD no aparece en el hot path.

### 2. Poll del contador de generación

Cada instancia corre un `BackgroundService` que cada 10 s hace
`SELECT value FROM settings_generation`. Si no cambió, no hace nada. Si cambió,
recarga el snapshot. Query trivial, funciona con **cualquier número de réplicas**,
robusto ante todo (incluidos `NOTIFY` perdidos, si algún día se agrega). Con 1
réplica igual hace falta: la escritura y la lectura pueden estar en requests
distintos, y el reinicio de un worker no debe perder estado.

Un `PUT` invalida además el caché local de su propia instancia al instante, así
que quien hizo el cambio lo ve reflejado sin esperar el poll.

### 3. Carril rápido de kill-switches

Las definiciones con `killSwitch: true` (`platform.maintenance_mode`,
`platform.contingency_mode`, `submission.fast_path.enabled`,
`submission.worker.enabled`) se leen con TTL de 5 s en vez de salir del snapshot
normal. Son pocas y su lectura es barata; en un incidente el corte no puede
esperar 10 s.

### Diferido — `LISTEN`/`NOTIFY`

Bajar la propagación de ~10 s a sub-segundo. El `PUT` haría
`NOTIFY settings_changed, '<key>'` en su transacción y cada instancia en `LISTEN`
(Npgsql lo soporta) evictaría al recibirlo; el poll queda de red de seguridad.
**No se construye ahora** — es una optimización de latencia pura, no un requisito
de correctitud, y el poll de 10 s ya es multi-réplica-safe. Se agrega el día que
10 s se sienta lento o el volumen lo justifique.

### Degradación

- Valor **corrupto** en una fila (no parsea contra su definición) → esa key cae a
  su default; se loguea y se cuenta como métrica.
- Almacén **inalcanzable** en una recarga → se mantiene el snapshot vigente
  (último bueno conocido); log + métrica + alarma. **Nunca** se regresa a los
  defaults de código por un fallo transitorio — eso apagaría `MaintenanceMode` en
  medio de un incidente.
- Arranque en frío con la BD caída → los defaults de código son el piso, y el
  `ValidateOnStart` del bootstrap ya garantizó lo crítico.

### El seam

`ISettingsCacheInvalidator` abstrae el transporte de invalidación. Hoy: poll del
contador + invalidación local en el `PUT`. Mañana se le enchufa `LISTEN`/`NOTIFY`
o Redis sin tocar un solo call site.

---

## Escritura

| Endpoint | Política | Efecto |
|---|---|---|
| `GET /api/v1/platform/settings` | `Operator` (`X-Admin-Key`) | lista **definiciones** (no filas) con valor efectivo y `resolvedFrom` |
| `PUT /api/v1/platform/settings/{key}` | `Operator` | sobrescribe (capa 2) |
| `DELETE /api/v1/platform/settings/{key}` | `Operator` | quita la sobrescritura → vuelve al default |
| `GET /api/v1/tenants/{id}/settings` | `TenantConfig` | definiciones scope `Tenant` + valor efectivo |
| `PUT` / `DELETE .../settings/{key}` | `TenantConfig` | solo definiciones con `TenantWritable`; RLS |

- **La validación la hace la definición** (`definition.Validate(rawValue)`), no el
  comando. El `UpdateSettingUseCase` mapea, resuelve el autor, escribe fila +
  bitácora + `NOTIFY` + bump de generación en una transacción
  (`IUnitOfWork.ExecuteInTransactionAsync`).
- **Guardrails**: los límites (`min`/`max`/opciones/regex) viven en la definición;
  un `PUT` fuera de rango se rechaza con `Error.Validation` → 400.
- **Settings sensibles** (`Sensitive = true`): el `PUT` exige `?confirm=true`; la
  UI muestra un modal. (Aprobación 4-ojos = futuro.)
- **Auto-chequeo al arrancar**: un `IValidateOptions`-equivalente recorre
  `platform_settings` y loguea (sin fallar) toda fila que ya no parsee contra su
  definición actual — está siendo ignorada y alguien debe saberlo.
- **Alerta por ráfaga**: N cambios de setting en una ventana corta → webhook /
  aviso (posible error o `X-Admin-Key` comprometida).

---

## Seguridad

- **Frontera con secretos**: nada security-sensitive detrás de un `PUT` de
  runtime. Los secretos rotan por Container Apps / Key Vault. Un setting **puede
  referenciar** el nombre de un secreto, nunca contenerlo.
- **RBAC**: escritura de plataforma = `Operator`; escritura de tenant =
  `admin_tenant` vía `TenantConfig`; lectura de plataforma = `Operator`. Alineado
  con [`api-auth.md`](api-auth.md).
- **Bitácora inmutable** (solo `INSERT`) + auditoría automática del endpoint
  `[Authorize]` ([`audit-log.md`](audit-log.md)).
- **RLS** en `tenant_settings`: un contribuyente no ve ni toca los settings de otro.

---

## Observabilidad

- Log estructurado en cada recarga: `{ generation, trigger, changed_keys, duration_ms }`.
- Métrica con la **generación efectiva por instancia**. Con `minReplicas = 1` sirve
  para confirmar que el poll está vivo; cuando se escale, un check externo alarma
  si las réplicas divergen más de N segundos (detección de drift).
- Contador de `fallback_to_default` por key → señala una fila corrupta o un deploy malo.
- El `GET` devuelve `resolvedFrom` (`default` / `platform` / `plan` /
  `subscription` / `tenant`) por setting — depurable, como `vercel env` mostrando el scope.

---

## Feature flags

Para on/off simples, un `BooleanSetting` alcanza. Si la matriz crece (segmentación
por plan, rollout gradual por porcentaje, ventanas horarias), el subtipo
`SettingDefinition` admite variantes multivaluadas, y
`Microsoft.FeatureManagement` da un `IFeatureManager` limpio con un
`IFeatureDefinitionProvider` custom sobre el mismo almacén. No adoptarlo antes de
necesitarlo; la jerarquía de definiciones ya deja la puerta abierta.

---

## Pruebas

- **Unitarias**: cada `SettingDefinition` valida/parsea/formatea round-trip; una
  prueba recorre `SettingDefinitions.All` y afirma `def.Validate(def.Default)` para
  todas (atrapa typos en defaults).
- **Unitarias**: el resolvedor elige la capa correcta para cada `Scope`.
- **Integración** (`[RequiresDockerFact]`): `PUT` → bump de generación → una
  segunda "instancia" (o una recarga forzada) ve el valor nuevo; RLS en
  `tenant_settings` corta cross-tenant; una fila corrupta cae al default sin lanzar.

---

## Quick win — `IOptionsMonitor` en los workers ✅ hecho

1. Los workers (`EcfSubmissionWorker`, `WebhookDeliveryWorker`,
   `ExpiryMonitorWorker`) y los pumps (`EcfSubmissionPump`, `WebhookDeliveryPump`)
   pasaron de `IOptions<T>` a `IOptionsMonitor<T>` y releen `.CurrentValue` al
   inicio de cada iteración / tick.
2. Las proyecciones `EcfSubmissionSettings` / `WebhookSettings` dejaron de ser
   singleton: `AddTransient` sobre `IOptionsMonitor<...>.CurrentValue.ToSettings()`.
   `WebhookSettings` alimenta un pump singleton y la config de un `HttpClient`, así
   que además `WebhookDeliveryPump` toma `IOptionsMonitor<WebhooksOptions>` y
   proyecta por tick, y `HttpWebhookUrlPolicy` (`IWebhookUrlPolicy`) pasó a
   `transient` para no capturar el valor.

Hoy no cambia nada observable (el origen sigue siendo `appsettings` estático),
pero cuando el config se sirva desde el almacén runtime la recarga "simplemente
funciona". `Program.cs` § "Envío a la DGII" / "Webhooks".

---

## Orden de trabajo

1. ~~Quick win de `IOptionsMonitor` en los workers.~~ ✅
2. Motor de settings: `SettingDefinition<T>` **con toda la metadata de
   presentación** (`Group`, `Label`, `Description`, `Unit`, orden) + registro por
   reflexión + `ISettingsReader` + `CachedSettingsReader` + `platform_settings` +
   bitácora + `settings_generation` + poll de 10 s. Sin `NOTIFY`.
3. Endpoints de plataforma completos: `GET` que proyecta **definiciones** (no
   filas) agrupadas, con valor efectivo y `resolvedFrom`; `PUT` / `DELETE` con
   validación en la definición. Primeras definiciones: kill-switches y **modo
   contingencia** (antes de M11).
4. **Pantalla de settings en el dashboard del operador** — consume el `GET`
   agrupado y renderiza un control por tipo. La metadata del paso 2 es justo lo
   que necesita; se construye junto con el resto del dashboard, no después.
5. `plans` / `tenant_subscriptions` como capas 3–4, con el módulo de medición.
6. `tenant_settings` (scope `Tenant`, RLS) + su pantalla en el dashboard del
   contribuyente, con M15.
7. *(Diferido)* `LISTEN`/`NOTIFY` cuando 10 s de propagación se sienta lento.

---

## Fuera de alcance

- `LISTEN`/`NOTIFY` y multi-réplica — el poll de 10 s con `minReplicas = 1` es el
  punto de partida; ambos son upgrades sin cambio de diseño.
- Aprobación 4-ojos / flujo de cambios para settings sensibles.
- Rollback de settings más allá de la bitácora (revertir a un punto en el tiempo).
- Programar cambios a futuro (ventanas de mantenimiento).
- Config por-ambiente exhaustiva — la columna `environment` existe, pero la mayoría
  de definiciones arrancan agnósticas.

---

## Referencias

- **Vercel** — *Environment Variables* (por ambiente, cifradas, sensibles
  write-only, efecto al re-desplegar) vs. *Edge Config* (KV de baja latencia para
  flags / redirects / maintenance-mode, escritura propagada global en <1 s, lectura
  desde una réplica local sin round trip). La dicotomía bootstrap/runtime de este doc.
- **Netflix Archaius** — propiedades dinámicas tipadas, polling de la fuente,
  defaults en código y *persisted fallback* para arrancar con valores recientes
  durante una caída.
- **Meta Configerator** — config como código, compilada + validada + revisada,
  distribuida con versión y canario antes del rollout global.
- **LaunchDarkly / OpenFeature** — targeting en capas (default → entorno → segmento
  → individuo), streaming a los SDK con polling de respaldo, evaluación local.
