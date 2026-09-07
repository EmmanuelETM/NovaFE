# Certificados digitales

Cada contribuyente sube su certificado INDOTEL (`.p12`/`.pfx`) por ambiente de la
DGII. NovaFE lo custodia (Modelo 2 — SaaS) y lo usa para firmar los e-CF y la
semilla de autenticación.

## Modelo

- `Certificate` (dominio) guarda **solo metadatos**: titular, subject, issuer,
  huella, ventana de validez, ambiente, estado, y una `VaultReference` **opaca**.
- El PKCS#12 en sí vive en el **vault**, detrás de `ICertificateVault`.
- Regla: a lo sumo un certificado activo por `(tenant, ambiente)`. Para reemplazar
  uno, se revoca y se sube el nuevo.
- `Certificate` es `ITenantOwned` → aislamiento por tenant (filtro de EF + RLS),
  ver [`multi-tenancy.md`](multi-tenancy.md).

## Validaciones al subir (reglas de la DGII)

`CertificateInspector` abre el PKCS#12 y `Certificate.Issue` valida:

1. Tiene clave privada.
2. Está dentro de su ventana de validez (`now` viene del `TimeProvider`).
3. El componente **SERIALNUMBER** (OID 2.5.4.5) del Subject —donde los
   certificados INDOTEL ponen el RNC/cédula— coincide con el RNC del
   contribuyente. Se comparan solo los dígitos (tolera prefijos como `RNC` y
   separadores).

> ⚠️ El punto 3 está implementado contra la convención esperada, pero **hay que
> verificarlo con un certificado real de TestECF**: si INDOTEL pone el RNC en
> otro campo del Subject, se ajusta `CertificateInspector.ReadHolderIdentifier`.

## El vault: qué proveedor, sin que importe

`ICertificateVault` es la costura. La implementación por defecto,
`EnvelopeCertificateVault`, es **portable a cualquier hosting**:

```
PKCS#12 + contraseña
  → AES-256-GCM con una clave de datos (DEK) aleatoria por secreto
  → la DEK se envuelve con la KEK vía IKeyProtector
  → { ciphertext, DEK envuelta, nonce, tag } se guarda en la tabla certificate_secrets
```

La base solo ve ciphertext. La KEK nunca toca la base — es la misma propiedad de
seguridad que Supabase Vault, pero sin atarse a Supabase.

| Pieza | Interfaz | Opciones | Se elige con |
|---|---|---|---|
| Dónde vive el PKCS#12 | `ICertificateVault` | `EnvelopeCertificateVault` (ciphertext en Postgres) — hoy la única. Después: `HashiCorpVaultCertificateVault` | registro en `InfrastructureService` |
| La clave que lo protege (KEK) | `IKeyProtector` | `LocalKeyProtector` (KEK en config) · `AzureKeyVaultKeyProtector` (RSA wrap en Key Vault). Después: `AwsKmsKeyProtector`, `GcpKmsKeyProtector` | `CertificateVault:Provider` |

`CertificateVault:Provider` (`local` \| `azure-key-vault`) elige el
`IKeyProtector` **por configuración**, sin tocar código.
`CertificateVaultOptionsValidator` valida al arrancar que estén los campos que
ese proveedor necesita. Agregar un proveedor nuevo = una implementación de
`IKeyProtector` + un `case` en el switch de `InfrastructureService`. Ni el dominio
ni la aplicación se enteran.

`AzureKeyVaultKeyProtector` usa `RSA-OAEP-256` contra una clave del vault; la
credencial la da `DefaultAzureCredential` (managed identity en Azure Container
Apps; `az login` en local). La KEK nunca entra en el proceso. Ver
[`deployment.md`](deployment.md).

## Configuración

`CertificateVault:Provider` — `local` (por defecto) o `azure-key-vault`.

**Provider `local`** — `CertificateVault:MasterKey`, KEK en base64 de
**exactamente 32 bytes** (AES-256).

| Entorno | Dónde |
|---|---|
| Local | `appsettings.Development.json` (valor de ejemplo, solo dev) o `dotnet user-secrets set "CertificateVault:MasterKey" "<base64>"` |
| Pruebas | `ApiFactory` la inyecta |
| Producción con `local` | Variable de entorno `CertificateVault__MasterKey` |

Generar una: `openssl rand -base64 32`.

**Provider `azure-key-vault`** — `CertificateVault:KeyVaultKeyUri`, la URI de la
clave RSA, p. ej. `https://novafe-kv.vault.azure.net/keys/cert-kek`. No hace falta
`MasterKey`. La credencial es implícita (`DefaultAzureCredential`).

`ValidateOnStart` + `CertificateVaultOptionsValidator`: una configuración
incoherente (p. ej. `local` sin `MasterKey`, o `azure-key-vault` sin URI) impide
el arranque.

## Cambiar de proveedor de KEK con certificados ya cargados

Un despliegue nuevo (sin certificados) cambia de proveedor solo con la
configuración. Pero si ya hay filas en `certificate_secrets`, su DEK está envuelta
con la KEK vieja — cambiar `Provider` sin más las dejaría **irrecuperables**.

Hay un paso de re-envoltura para eso. La DEK y el ciphertext del PKCS#12 no
cambian; solo `wrapped_key`:

1. Configurar la app con el proveedor **nuevo** (`CertificateVault:Provider` +
   sus campos) y, además, la config del **viejo** bajo `CertificateVault:Rewrap`:

   | Clave | |
   |---|---|
   | `CertificateVault:Rewrap:Enabled` | `true` — dispara el modo y sale |
   | `CertificateVault:Rewrap:FromProvider` | `local` o `azure-key-vault` |
   | `CertificateVault:Rewrap:FromMasterKey` | si el viejo era `local` |
   | `CertificateVault:Rewrap:FromKeyVaultKeyUri` | si el viejo era `azure-key-vault` |

2. Correr la imagen una vez con esa configuración (un Container Apps Job, igual
   que las migraciones). Itera tenant por tenant, verifica que la KEK vieja
   descifra cada fila, re-envuelve con la nueva, y termina. Es todo-o-nada por
   tenant (una transacción).
3. Quitar la sección `CertificateVault:Rewrap` y desplegar normal.

Cubierto por `CertificateSecretRewrapTests` (round-trip real + que una KEK de
origen equivocada aborta sin tocar nada).

## Roadmap del vault (de `contexto-proyecto-fe-dgii.md` §11)

- **Fase 1 (0–15 clientes):** `EnvelopeCertificateVault` + `LocalKeyProtector`
  con la KEK en variable de entorno. Aceptable para la certificación DGII.
- **Fase 1.5 (implementada):** mismo vault, `IKeyProtector` → Azure Key Vault
  (`CertificateVault:Provider=azure-key-vault`). La KEK vive en el vault y la
  envoltura/desenvoltura de la DEK ocurre allí; la KEK nunca entra en el proceso.
  `AwsKmsKeyProtector` / `GcpKmsKeyProtector` quedan como el mismo patrón para
  otros clouds. Ver [`deployment.md`](deployment.md).
- **Fase 2 (~15–50 clientes):** evaluar HashiCorp Vault con Transit Engine — la
  clave privada nunca entra en memoria de la app (RF-03.7). Es una
  implementación nueva de `ICertificateVault`, no una migración de datos
  traumática.

## Pendiente

- Alertas de vencimiento (90/30/15/7 días) — RF-01.6. Será el primer worker
  in-process (ver el flag `Workers:Enabled` cuando exista).
- Purga de `certificate_secrets` de certificados revocados hace > N días.
- Verificar `HolderIdentifier` contra un certificado real de TestECF.
