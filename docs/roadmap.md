# Roadmap

Backlog vivo de lo que falta. Nada de esto es código a medio escribir — el repo
mergea slices completos a `main`; todo lo de abajo es **scope diferido a
propósito** y está documentado en el `docs/*.md` de cada módulo.

Referencia de módulos: `Plan Técnico Integral v2.0` (`C:\workplace\FE_DGII\`).
Estado de los módulos ya construidos: la sección de arquitectura de `CLAUDE.md`.

**Construido:** M1 (parcial), M2, M3, M4, M6 (núcleo), M7 (v1), M9, M12, M13,
M14. **Sin empezar:** M5, M8 (parcial), M10 (parcial), M11, M15.

---

## P0 — Antes de cobrarle a un cliente real

Bloquean el primer cliente o la certificación con la DGII.

| Ítem | Detalle | Doc |
|---|---|---|
| **Probar el despliegue** | Etapas 0 (Neon local) → 1 (Azure + KEK local) → 2 (Key Vault). Sin esto no hay nada que vender. | `docs/deployment.md`, `deploy/README.md`, memoria `deploy-testing-plan` |
| **Verificar contra TestECF real** | Formato exacto de los campos de las 3 respuestas de la DGII (`trackId`, `codigo` número vs. cadena), comportamiento del código `0`, resolución síncrona del RFCE, ruta exacta de `recepcionfc`, `HolderIdentifier` en el subject del certificado INDOTEL. La certificación DGII depende de que esto sea correcto. | `docs/dgii-submission.md` §"Pendiente de verificar", `docs/certificates.md` |
| **Rol `novafe_app` + prueba de aislamiento RLS** | El script SQL existe (`deploy/sql/001-app-role.sql`); falta la prueba de integración que se conecta como rol no-superusuario y verifica el corte cross-tenant. Hoy las pruebas corren como `postgres` y RLS nunca se ejercita. | `docs/multi-tenancy.md` |
| **Worker de alertas de vencimiento de certificados (RF-01.6)** | 90/30/15/7 días. Un cliente cuyo certificado vence en silencio no puede facturar. Sería el primer worker in-process (`Workers:Enabled`). | `docs/certificates.md` §"Pendiente" |

## P1 — Primeros clientes / requisito regulatorio

| Ítem | Detalle | Doc |
|---|---|---|
| **M15 — Panel de administración** | Dashboard (Next.js) + auth humano en el backend. Un cliente necesita ver sus e-CF y configurarse sin que el operador lo haga a mano por la API. El plan del auth está escrito, sin implementar (ahora con BetterAuth como emisor). | plan `linear-beaming-squirrel.md`, memoria `human-auth-plan` |
| **M11 — Contingencia (Decreto 587-24)** | Modo contingencia cuando la DGII está caída, `IndicadorEnvioDiferido`, estado `contingency_pending`, leyenda verbatim en la RI (RF-09.5). Requisito legal en RD — confirmar si la DGII lo exige para certificar. | Plan Técnico §12; `docs/representation.md` ya deja un `ContingencyNotice?` opcional |
| **M5 — Endpoints B2B (receptor) + ARECF (RF-02.7)** | Exponer `/fe/autenticacion/api/semilla`, `/fe/.../validacioncertificado`, `/fe/recepcion/api/ecf` para que otros contribuyentes nos manden e-CF; generar el **ARECF firmado** (acuse de recibo). Al recibir un e-CF hay obligación legal de responder el ARECF. | Plan Técnico §6 |

## P2 — Operación y completitud

| Ítem | Detalle | Doc |
|---|---|---|
| **M8 — Anulación de rango (ANECF) + estado `voided`** | Las notas de crédito (tipo 34) ya funcionan como e-CF vía M12; falta la anulación de un rango de secuencias y el estado `voided`. | Plan Técnico §9; `docs/api-ecf.md`, `docs/sequences.md` |
| **M10 — `consultaestatusservicio` + consultas restantes** | `consultaresultado` ya se usa en M4. Falta el "¿está viva la DGII?" (lo necesita M11), consulta de directorio, TrackIds masivo. | Plan Técnico §11; `docs/dgii-submission.md` |
| ~~Webhooks (HMAC-SHA256, RF-12.7)~~ | **Hecho.** 6 eventos de ciclo de vida del e-CF + suscripciones por tenant + outbox de entrega + firma HMAC + log de entregas. | `docs/webhooks.md` |
| **M12 — matriz de obligatoriedad 0/1/2/3 por tipo** | Completar los validadores por tipo (`IssueEcfCommandValidator`); hoy la matriz autoritativa vive solo en `EcfDocument.ValidateStructure`. | `docs/api-ecf.md`, `docs/ecf-xml.md` |
| **Rate limiting por plan (RF-12.3)** | El limiter global ya particiona por tenant; los topes por plan son otro slice. | `docs/api-auth.md` §"Fuera de alcance" |

## P3 — Cuando un cliente lo pida, o cuando duela

**Fiscal / XML** (`docs/fiscal.md`, `docs/ecf-xml.md`) — hoy passthrough (el cliente trae los montos):
- Derivar el ISC específico desde `GradosAlcohol` / `CantidadReferencia`.
- ISC ad valorem y su interacción con la base del ITBIS (RF-06.4 pasos 3–5).
- **Cálculo** de las tasas de retención de ITBIS/ISR.
- Distribución proporcional de la Sección D a nivel de línea (Formato notas 28/29).
- `TablaSubDescuento` / `TablaSubRecargo` a nivel de línea.
- Bloque anidado `ImpuestosAdicionalesOtraMoneda`.
- Chequeo sano `ItbisRetenido ≤ TaxAmount` de la línea.

**Secuencias** (`docs/sequences.md`):
- Pool de secuencias liberadas (reclamar números quemados por un rechazo con `secuenciaUtilizada = false`).
- Ciclo de vida por secuencia (`asignada → firmada → enviada → aceptada | rechazada`, RF-07.5).

**Representación Impresa** (`docs/representation.md`):
- Correo al comprador con `<CorreoComprador>` (RF-09.7) — necesita infra de correo.
- Pulido del layout Carta; conformidad PDF/A; catálogos de Tabla III y unidades de medida; accent color / logo por tenant.

**Seguridad** (`docs/api-auth.md`):
- Scopes por key más finos que el rol; rotación de key con período de gracia; cambiar ambiente/rol de una key existente.

**Higiene / operación:**
- Purga de `certificate_secrets` de certificados revocados hace > N días (`docs/certificates.md`).
- Retención / archivado / purga del `audit_log`; filtros en el listado (acción, fechas, actor) (`docs/audit-log.md`).
- Validar los umbrales de niveles de e-CF y la elasticidad de precio con datos de pilotos (`docs/pricing.md`).
