# Firma XMLDSig

El componente más frágil del proyecto. Cualquier desviación de los parámetros de
la DGII produce firmas que **rechaza sin mensaje de error claro** (los
competidores PHP tienen un bug silencioso justo aquí: `C14N(true,false)` en vez
de `C14N(false,false)`).

Fuente: `C:\workplace\FE_DGII\` — "Firmado de e-CF.pdf" y
`contexto-proyecto-fe-dgii.md` §5.8 y §5.11.

## Piezas

| Interfaz | Impl | Rol |
|---|---|---|
| `IXmlSigner` (Application) | `XmlDsigSigner` (Infrastructure) | Cripto pura: firma / verifica un XML dado un `X509Certificate2`. Sin DGII, sin base de datos. Singleton. |
| `ICertificateSigner` (Application) | `CertificateSigner` (Application) | Orquesta: certificado activo del tenant → PKCS#12 del vault → valida vigencia → `IXmlSigner` → limpia el material. Devuelve `ErrorOr`. |

`SignedXmlResult`: `Xml` (firmado), `SignatureValue` (Base64), `SecurityCode`
(primeros 6 chars del `SignatureValue` — es el `CodigoSeguridad` del QR / la RI).

## Parámetros que NO se tocan

Verificados en `XmlDsigSignerTests` (se afirman los URIs literales).

| Elemento | Valor | Nota |
|---|---|---|
| CanonicalizationMethod | `http://www.w3.org/TR/2001/REC-xml-c14n-20010315` | C14N **estándar** (inclusive). **NO** exclusive (`#exc-c14n`). Es el default de .NET, pero se fija explícito. |
| SignatureMethod | `http://www.w3.org/2001/04/xmldsig-more#rsa-sha256` | RSA-SHA256 |
| DigestMethod | `http://www.w3.org/2001/04/xmlenc#sha256` | SHA-256 |
| Transform | `http://www.w3.org/2000/09/xmldsig#enveloped-signature` | |
| `Reference URI` | `""` (cadena vacía) | Firma el documento completo |
| `PreserveWhitespace` | `false` | Con indentación la firma sale inválida. `XmlDsigSignerTests.Sign_ignores_input_indentation` lo verifica: mismo `DigestValue` con XML compacto y con XML indentado. |
| Certificado | embebido en `KeyInfo/X509Data/X509Certificate` (DER en Base64) | La DGII no acepta referencias externas al certificado. |
| Posición de `<Signature>` | último hijo del elemento raíz (`<ECF>`) | |

**`CspParameters(24)` del ejemplo C# de la DGII: no se usa.** Es un workaround de
Windows CSP legacy. `X509Certificate2.GetRSAPrivateKey()` en .NET moderno devuelve
`RSAOpenSsl` (Linux) o `RSACng` (Windows), que ya soportan SHA-256.

## Manejo de la clave privada

- El PKCS#12 se carga con `X509KeyStorageFlags.EphemeralKeySet` — la clave nunca
  se escribe a un almacén del SO.
- `CertificateSecret` (lo que devuelve el vault) hace `CryptographicOperations.ZeroMemory`
  al `Dispose`. `CertificateSigner` lo envuelve en `using`.
- El `X509Certificate2` y el `RSA` se disponen en cuanto termina la firma.
- Fase 2 (RF-03.7): HashiCorp Vault Transit — la clave privada nunca entra en
  memoria de la app. Es una implementación nueva de `IXmlSigner` /
  `ICertificateSigner`, no un cambio de arquitectura.

## Verificación (`IXmlSigner.Verify`)

Para validar la firma de e-CF recibidos de otros contribuyentes (RF-03.6, antes
de generar el ARECF). Verifica contra el certificado **embebido en `KeyInfo`**
(el modelo de la DGII). Endurecimiento aplicado: `XmlResolver = null` (XXE), y se
exige exactamente una `<Signature>` que cuelgue directamente de la raíz
(anti signature-wrapping). Cuando se construya el receptor B2B hace falta una
pasada más completa (validar cadena de confianza, que el firmante esté autorizado).

## Lo que falta verificar contra la DGII real

Todo lo verificable sin la DGII está en verde. Lo único que solo confirma un
envío real a TesteCF:

1. **Que su validador acepte nuestra canonicalización byte a byte.** Construimos
   a especificación y `.NET` produce C14N estándar; hay un riesgo residual chico
   con el manejo de namespaces de `SignedXml`.
2. **`CodigoSeguridad`** — el plan (RF-03.5, RF-09.1) y §5.8 dicen "primeros 6
   chars del Base64 de `<SignatureValue>`". Confirmar contra un e-CF de ejemplo
   de la DGII.
3. **Firma de la semilla de autenticación** — mismo `IXmlSigner`, pero la semilla
   tiene su propio formato. Se prueba al conectar con TesteCF.
