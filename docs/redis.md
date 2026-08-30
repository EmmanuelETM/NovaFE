# Añadir Redis (cuando haga falta)

NovaFE arranca **sin Redis**. La caché distribuida es en memoria
(`AddDistributedMemoryCache`), suficiente mientras la API corra en una sola
instancia. Este documento describe qué se decidió, cuándo reconsiderarlo y los
pasos exactos para volver a meter Redis sin tocar a los consumidores.

## Por qué no está desde el día uno

A la escala de arranque (los 3 primeros clientes son pilotos técnicos; el
throughput lo limita la latencia de DGII de ~13,6 s, no la velocidad de la
caché) Redis es superficie de mantenimiento sin beneficio:

- otro servicio en compose, otro healthcheck, otro orden de arranque;
- otro connection string / secreto **por ambiente** (TesteCF, CerteCF, prod);
- otro componente que provisionar, monitorear y meter en el runbook;
- otro modo de fallo ("Redis caído: ¿la API está degradada o muerta?");
- semántica de expiración que razonar (una clave de idempotencia desalojada por
  presión de memoria = doble emisión silenciosa).

Cada caso de uso que el plan técnico asignaba a Redis tiene mejor hogar:

| Caso de uso | Hogar correcto a esta escala | Por qué no Redis |
|---|---|---|
| Caché del token DGII (1 h, por tenant+ambiente) | `IDistributedCache` en memoria; una fila en Postgres si hay >1 réplica o se quiere no re-autenticar tras cada deploy | Re-pedir el token es una llamada barata a DGII |
| Claves de idempotencia (`X-Idempotency-Key`, 24 h) | **Tabla en PostgreSQL**, única por `(tenant_id, key)`, guardando la respuesta original | Debe ser durable y auditable; un desalojo = bug de correctitud |
| Lock de asignación de secuencia e-NCF | **`SELECT … FOR UPDATE`** sobre la fila de `secuencias_ecf`, en la misma transacción que el incremento | Un lock en Redis (Redlock) es *más débil*: lock y escritura no son atómicos |
| Directorio de facturadores (24 h) | Tabla en Postgres o `IDistributedCache` en memoria | Cambia rara vez |
| Semillas B2B (5 min) | `IDistributedCache` en memoria | Efímeras; si otra réplica no la tiene, el llamador reintenta |
| Rate limiting por tenant | En memoria (una instancia); Cloudflare para límites gruesos | ASP.NET no trae limiter de Redis de fábrica |

## Cuándo reconsiderarlo

Meter Redis de forma deliberada, con evidencia, cuando se cumpla alguna:

1. **Se corren 2+ réplicas de la API** y se midió que las cachés en memoria por
   réplica o los viajes a Postgres están doliendo de verdad.
2. Se añade **SignalR** para el dashboard en vivo y hace falta backplane entre
   réplicas (Postgres `LISTEN/NOTIFY` también sirve).
3. La carga de lectura de Postgres por caché de hot-path se vuelve cuello de
   botella real (muy lejos).

## Cómo volver a meterlo

Cambio aislado, ~medio día, sin migración de datos ni downtime.

### 1. Paquetes (`Directory.Packages.props`, grupo "Caché")

```xml
<PackageVersion Include="StackExchange.Redis" Version="3.1.31" />
<PackageVersion Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="10.0.11" />
<!-- y en "Health checks": -->
<PackageVersion Include="AspNetCore.HealthChecks.Redis" Version="9.0.0" />
```

Referenciarlos: `StackExchange.Redis` + `Microsoft.Extensions.Caching.StackExchangeRedis`
en `src/Infrastructure/NovaFE.Infrastructure.csproj`; `AspNetCore.HealthChecks.Redis`
en `src/Service/NovaFE.Service.csproj`.

### 2. Registro (`src/Infrastructure/Caching/CacheExtensions.cs`)

Reemplazar el cuerpo de `AddCache`:

```csharp
internal static IServiceCollection AddCache(this IServiceCollection services, IConfiguration configuration)
{
    var redis = configuration.GetConnectionString("Redis");

    if (string.IsNullOrWhiteSpace(redis))
    {
        services.AddDistributedMemoryCache();   // fallback local / pruebas
        return services;
    }

    var config = ConfigurationOptions.Parse(redis);
    config.AbortOnConnectFail = false;          // la app arranca aunque Redis esté caído
    var multiplexer = ConnectionMultiplexer.Connect(config);

    services.AddSingleton<IConnectionMultiplexer>(multiplexer);
    services.AddStackExchangeRedisCache(o =>
    {
        o.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(multiplexer);
        o.InstanceName = "novafe:";
    });

    return services;
}
```

Pasar `configuration` en la llamada de `InfrastructureService.AddInfrastructure`.

### 3. Health check (`src/Service/Extensions/HealthCheckExtensions.cs`)

```csharp
var redis = configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redis))
{
    healthChecks.AddRedis(redis, name: "redis", failureStatus: HealthStatus.Unhealthy,
        tags: [TagReady], timeout: TimeSpan.FromSeconds(5));
}
```

### 4. Configuración

- `appsettings.json`: `"ConnectionStrings": { "Default": "", "Redis": "" }`
- `appsettings.Development.json`: `"Redis": "localhost:6379,abortConnect=false"`
- `tests/IntegrationTests/Fixtures/ApiFactory.cs`: añadir `["ConnectionStrings:Redis"] = ""`
  para que las pruebas de integración no dependan de Redis.
- Producción: connection string del Redis gestionado (Upstash, o Redis/Valkey del
  proveedor) como variable de entorno `ConnectionStrings__Redis`.

### 5. Compose (solo para local)

En `docker-compose.yml`:

```yaml
  redis:
    image: redis:7-alpine
    command: ["redis-server", "--appendonly", "yes", "--save", "60", "1"]
    volumes: [redisdata:/data]
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 10
      start_period: 5s
    restart: unless-stopped
# ...
volumes:
  pgdata:
  redisdata:
```

Añadir `ConnectionStrings__Redis: "redis:6379,abortConnect=false"` al `environment`
del servicio `api` y `redis: { condition: service_healthy }` a su `depends_on`.

En `docker-compose.override.yml`, publicar el puerto y (opcional) el
redis-commander bajo el perfil `tools`:

```yaml
  redis:
    ports: ["6379:6379"]

  redis-commander:
    image: rediscommander/redis-commander:latest
    profiles: ["tools"]
    environment:
      REDIS_HOSTS: "local:redis:6379"
    ports: ["8082:8081"]
    depends_on:
      redis:
        condition: service_healthy
```

## Regla para que siga siendo barato

El acceso a caché/estado va **detrás de una interfaz** en `Application`
(p. ej. `IDgiiTokenCache`, `IDirectorioCache`), implementada en `Infrastructure`.
Los casos de uso y controllers dependen de la interfaz, nunca de
`IConnectionMultiplexer` ni de `IDistributedCache` directamente. Así, cambiar el
back-end es un archivo, no un refactor.

Lo que exige durabilidad o unicidad —idempotencia, lock de secuencias— **no** es
caché: va a PostgreSQL en cualquier versión de este sistema.
