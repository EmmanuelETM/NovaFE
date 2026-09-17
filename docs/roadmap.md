# Roadmap

Backlog vivo de lo que falta. Nada de esto es código a medio escribir — el repo
mergea slices completos a `main`; todo lo de abajo es **scope diferido a
propósito** y está documentado en el `docs/*.md` de cada módulo.

Referencia de módulos: `Plan Técnico Integral v2.0` (`C:\workplace\FE_DGII\`).
Estado de los módulos ya construidos: la sección de arquitectura de `CLAUDE.md`.

**Construido:** M1 (parcial), M2, M3, M4, M6 (núcleo), M7 (v1), M9, M10
(parcial — ver abajo), M11 (parcial — Tipo 1, ver abajo), M12, M13, M14, M15
(panel de operador completo: settings, consola de operación en vivo,
onboarding de contribuyentes; auth humano completo — OAuth Google/GitHub/
Microsoft + email/contraseña con verificación y reset; self-service del
contribuyente: listado y detalle de sus e-CF en `/comprobantes`).
**Sin empezar:** M5, M8 (parcial).

---

## P0 — Antes de cobrarle a un cliente real

Bloquean el primer cliente o la certificación con la DGII.

| Ítem | Detalle | Doc |
|---|---|---|
| **Probar el despliegue** | Etapas 0 (Neon local) → 1 (Azure + KEK local) → 2 (Key Vault). Sin esto no hay nada que vender. | `docs/deployment.md`, `deploy/README.md`, memoria `deploy-testing-plan` |
| **Verificar contra TestECF real** | Formato exacto de los campos de las 3 respuestas de la DGII (`trackId`, `codigo` número vs. cadena), comportamiento del código `0`, resolución síncrona del RFCE, ruta exacta de `recepcionfc`, `HolderIdentifier` en el subject del certificado INDOTEL. La certificación DGII depende de que esto sea correcto. | `docs/dgii-submission.md` §"Pendiente de verificar", `docs/certificates.md` |
| Prueba de aislamiento RLS con rol restringido | ✅ **Hecho.** `RowLevelSecurityTests` conecta como un rol sin `BYPASSRLS` y verifica el corte cross-tenant (lecturas, `WITH CHECK` en `INSERT`, `UPDATE`/`DELETE` de filas ajenas). Falta correr `deploy/sql/001-app-role.sql` en el Postgres de producción y apuntar el runtime a `novafe_app`. | `docs/multi-tenancy.md` |
| ~~Worker de alertas de vencimiento de certificados (RF-01.6)~~ | **Hecho.** `ExpiryMonitorWorker`: avisa por webhook `certificate.expiring` (90/30/15/7) · `certificate.expired` · `sequence.expiring`/`expired`/`low`/`exhausted`. | `docs/expiry-monitor.md` |

## P1 — Primeros clientes / requisito regulatorio

| Ítem | Detalle | Doc |
|---|---|---|
| ~~M15 — Panel de administración~~ | **Hecho.** Auth humano (BetterAuth self-hosted) completo: OAuth Google/GitHub/Microsoft + email/contraseña (registro, verificación de correo, reset). Pantallas de **operador**: settings, usuarios, consola de operación, onboarding de contribuyentes. Pantallas **self-service del contribuyente**: `/comprobantes` (listado + detalle con XML/RI/reintento) y `/configuracion` con pestañas de certificados, secuencias de e-NCF y webhooks (antes solo operables por un operador desde `/nemus/tenants/{id}`), visible a `consultor`/`emisor`/`admin_tenant` según la acción. | plan `linear-beaming-squirrel.md`, memoria `human-auth-plan` / `ops-console-and-onboarding` |
| ~~Consola de operación en vivo~~ | **Hecho.** `GET /api/v1/ops/status` (operador): heartbeat de los 5 workers, profundidad/antigüedad de los outbox (DGII + webhooks), tenants con secuencia por agotarse. Dashboard en `/plataforma/operacion`. | memoria `ops-console-and-onboarding` |
| ~~Onboarding de contribuyentes~~ | **Hecho.** Endpoints de operador para cargar certificado y rango de secuencia de un tenant nuevo (resuelve el candado circular de alta) + wizard de registro en 2 pasos en el dashboard. | memoria `ops-console-and-onboarding` |
| ~~M11 Tipo 1 — Contingencia por falta de conectividad~~ | **Hecho.** `platform.contingency_mode` (manual + detección automática sobre la antigüedad del outbox de envío), reintento sin dar por perdido mientras dure, leyenda verbatim en la RI, webhooks `contingency.activated`/`deactivated`. | `docs/contingency.md` |
| **M11 Tipo 2/3 — Contingencia por imposibilidad técnica / caída de la DGII** | Declaración manual vía OFV (Modalidad Total/Parcial), comprobantes Serie B, estado `contingency_pending`, plazo 15 días + regularización a 30. Confirmar si la DGII lo exige para certificar. | Plan Técnico §12; `docs/contingency.md` §"Fuera de alcance" |
| **M5 — Endpoints B2B (receptor) + ARECF (RF-02.7)** | Exponer `/fe/autenticacion/api/semilla`, `/fe/.../validacioncertificado`, `/fe/recepcion/api/ecf` para que otros contribuyentes nos manden e-CF; generar el **ARECF firmado** (acuse de recibo). Al recibir un e-CF hay obligación legal de responder el ARECF. | Plan Técnico §6 |

## P2 — Operación y completitud

| Ítem | Detalle | Doc |
|---|---|---|
| **M8 — Anulación de rango (ANECF) + estado `voided`** | Las notas de crédito (tipo 34) ya funcionan como e-CF vía M12; falta la anulación de un rango de secuencias y el estado `voided`. | Plan Técnico §9; `docs/api-ecf.md`, `docs/sequences.md` |
| ~~M10 — trackIds + directorio~~ | **Hecho.** `GET /api/v1/ecf/{id}/trackids` (endpoint del tenant); directorio construido como capacidad interna (sin endpoint — sin consumidor real hasta M5). No disponibles en CerteCF, por diseño de la DGII. | `docs/dgii-queries.md` |
| **M10 — estatus de servicio, sin verificar** | `IDgiiStatusClient` ya llama a los 3 endpoints reales (`statusecf.dgii.gov.do`, confirmados contra su OpenAPI público) y sale como diagnóstico de operador (`GET /api/v1/ops/dgii-status`) — pero la DGII no documenta el schema de la respuesta, así que el parseo es tolerante y nada depende de él todavía. Falta una `Dgii:StatusApiKey` real para confirmarlo. | `docs/dgii-queries.md` |
| ~~Webhooks (HMAC-SHA256, RF-12.7)~~ | **Hecho.** 6 eventos de ciclo de vida del e-CF + suscripciones por tenant + outbox de entrega + firma HMAC + log de entregas. | `docs/webhooks.md` |
| **M12 — matriz de obligatoriedad 0/1/2/3 por tipo** | Completar los validadores por tipo (`IssueEcfCommandValidator`); hoy la matriz autoritativa vive solo en `EcfDocument.ValidateStructure`. | `docs/api-ecf.md`, `docs/ecf-xml.md` |
| **Rate limiting por plan (RF-12.3)** | El limiter global ya particiona por tenant; los topes por plan son otro slice. | `docs/api-auth.md` §"Fuera de alcance" |
| ~~Backlog de 6 frentes de valores hardcodeados~~ | **Hecho.** Purga de `idempotency_keys`/`audit_log` (`RetentionWorker`) · tuning del circuit breaker de la DGII (`MinimumThroughput`/`FailureRatio`/`BreakDuration` reales) · ladders/lotes de envío y webhooks como settings runtime · umbrales de vencimiento de certificados/secuencias + `low_stock_fraction` · detección de duplicados de e-CF por huella (modo `bloquear`/`observar`/`off`) · tope de 7 formas de pago + de-dup de `CertMaxSequential`. | memoria `retention-purges-track` |
| ~~Quick wins de observabilidad~~ | **Hecho.** Heartbeat de los 5 workers + `WorkerLivenessHealthCheck`, y umbrales de request/query lenta como settings runtime (`observability.slow_request_threshold_ms`/`slow_query_threshold_ms`). | `docs/observability.md`, memoria `observability-performance` |
| **Decisiones de infraestructura de observabilidad** | A qué colector OTLP exportar, logs centralizados, sampling en producción, alerting real (umbral + canal), dashboards. Son decisiones del usuario, no código — sin resolverlas no hay dónde mandar ninguna métrica nueva. Debería alinearse con la decisión de stack de despliegue (Azure+Neon vs. VPS). | `docs/observability.md` §"Decisiones de infraestructura pendientes", `docs/vps-migration.md` |
| **Settings: `plans`/`tenant_subscriptions` (paso 6) + path de operador restante** | Capas 3–4 del motor de settings con el módulo de medición; y de tenant settings (paso 7, ya mergeado) falta solo `/api/v1/tenants/{id}/settings` para que el operador edite settings de un tenant ajeno. | `docs/configuration.md` §"Orden de trabajo" |

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
- Archivado del `audit_log` (la purga por antigüedad ya la hace `RetentionWorker`, ver P2); filtros en el listado (acción, fechas, actor) (`docs/audit-log.md`).
- Validar los umbrales de niveles de e-CF y la elasticidad de precio con datos de pilotos (`docs/pricing.md`).
