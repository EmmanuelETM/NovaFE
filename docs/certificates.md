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

| Pieza | Interfaz | Hoy | Después |
|---|---|---|---|
| Dónde vive el PKCS#12 | `ICertificateVault` | `EnvelopeCertificateVault` (ciphertext en Postgres) | `SupabaseVaultCertificateVault`, `HashiCorpVaultCertificateVault` |
| La clave que lo protege (KEK) | `IKeyProtector` | `LocalKeyProtector` (KEK de config) | `AwsKmsKeyProtector`, `GcpKmsKeyProtector`, `AzureKeyVaultKeyProtector` |

Cambiar de proveedor = una implementación nueva + una línea en
`InfrastructureService`. Ni el dominio ni la aplicación se enteran.

## Configuración

`CertificateVault:MasterKey` — KEK en base64, **exactamente 32 bytes** (AES-256).

| Entorno | Dónde |
|---|---|
| Local | `appsettings.Development.json` (valor de ejemplo, solo dev) o `dotnet user-secrets set "CertificateVault:MasterKey" "<base64>"` |
| Pruebas | `ApiFactory` la inyecta |
| Producción | Variable de entorno `CertificateVault__MasterKey`, o cuando se pase a KMS, el `IKeyProtector` correspondiente |

Generar una: `openssl rand -base64 32`.

`ValidateOnStart` está activo: sin `MasterKey` la app no arranca.

## Roadmap del vault (de `contexto-proyecto-fe-dgii.md` §11)

- **Fase 1 (0–15 clientes):** `EnvelopeCertificateVault` + `LocalKeyProtector`
  con la KEK en variable de entorno. Aceptable para la certificación DGII.
- **Fase 1.5:** mismo vault, `IKeyProtector` → KMS (AWS/GCP). La KEK pasa a estar
  respaldada por HSM y fuera de nuestro control directo. Cambio de una clase.
- **Fase 2 (~15–50 clientes):** evaluar HashiCorp Vault con Transit Engine — la
  clave privada nunca entra en memoria de la app (RF-03.7). Es una
  implementación nueva de `ICertificateVault`, no una migración de datos
  traumática.

## Pendiente

- Alertas de vencimiento (90/30/15/7 días) — RF-01.6. Será el primer worker
  in-process (ver el flag `Workers:Enabled` cuando exista).
- Purga de `certificate_secrets` de certificados revocados hace > N días.
- Verificar `HolderIdentifier` contra un certificado real de TestECF.
