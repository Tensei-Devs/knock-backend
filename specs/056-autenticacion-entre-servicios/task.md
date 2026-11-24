# Tareas 056 — Autenticación entre servicios (KNOCK-162)

> **Estado:** T-01 a T-07 y T-09 hechas o parciales en la rama `feature/KNOCK-266-credencial-servicio-unica` (conciliado el 2026-11-23); D-01 a D-04 decididas por José el 2026-11-23.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Decisiones de §8 registradas (2026-11-19); responder D-01 a D-04 (sí/no con recomendación, spec §8) | R-10 | hecha (D-01 a D-04: sí, `DECISIONES_PENDIENTES.md`, 2026-11-23) |
| T-02 | Definir o extraer la validación de la credencial de servicio (según P-01) con falla cerrada | R-01 a R-06 · CA-02, CA-06 | hecha (`src/Knock.Hosting/Security/ServiceCredential.cs`; Mercado, Mensajería e Identidad la usan) |
| T-03 | Aplicar la regla común en Mensajería (`POST /api/v1/interno/avisos`, spec 037) y revisar Mercado (spec 041) | R-01 a R-05, R-09 · CA-03, CA-09 | hecha (`InternalNoticeAuthentication` y `InternalServiceAuthentication` delegan en `Knock.Hosting`; sin cambio de contrato; suites 037 y 041 sin editar) |
| T-04 | Comprobar que el gateway no enruta `/api/v1/interno/*` | R-07 · CA-07 | hecha (`Spec056RutasInternasTests`) |
| T-05 | Escribir las pruebas de seguridad negativa (credenciales inválidas, JWT cruzado, emisor cruzado, sin permiso) | CA-01, CA-03, CA-04, CA-05 | hecha en código (`ServiceCredentialTests` CA-01, 02, 04, 05; `Spec037InternoAvisosSeguridadTests.InternalNotices_CredentialWithoutThePermission_Returns403ServiceNotAllowed_CA_056_03` es nueva y solo corre en el CI contra DocumentDB: falta ver su run verde) |
| T-06 | Pruebas de arquitectura y de no exposición de secretos | R-05, R-08 · CA-08 | parcial: `Spec056SecretosTests` (CA_056_08) comprueba que ningún `appsettings*.json` trae claves de servicio ni `SigningKey`, que el gateway no nombra las claves internas y que no tiene rutas `/interno`; falta probar el contenido de los registros de una llamada rechazada y aceptada, y la revisión de `security-reviewer` |
| T-07 | Replay (idempotente, sin `jti` guardado) y rotación (dos claves, 24 h) según P-03 y P-02 | CA-10, CA-11 | hecha (`ServiceCredentialTests` CA_056_10 y CA_056_11; repetición por ruta en `Spec041HistorialInternoTests` y `Spec037InternoAvisosSeguridadTests`; la repetición de las demás rutas internas queda pendiente) |
| T-10 | Dejar probado que Mensajería→Mercado (spec 026) reenvía el JWT del usuario | R-11 · CA-12 | parcial: `Spec056ReenvioDelJwtTests` (CA_056_12) prueba que `MarketVisitaCatalog` reenvía el JWT del usuario a la ruta pública y no reintenta con otra credencial tras 401/403; el 401 de Mercado ante una credencial de servicio en esa ruta sigue cubierto solo por la validación (`CA_056_04`), sin prueba de integración propia |
| T-11 | Enmienda a la spec 055 para el catálogo de `Knock.Hosting` (P-01b), en PR aparte y antes del código | P-01b | hecha en este PR (§14 punto 5 de la spec 055; sin aprobar hasta que las den las personas) |
| T-08 | Secretos (`avisos-key-*`, `interno-key-identity`) y permisos en Key Vault y Terraform | P-04 | hecha (conciliado 2026-11-23): `avisos-key-*` e `interno-key-*` cableados en `deploy/apps/main.tf` con el rol `Key Vault Secrets User` de `deploy/modules/service/main.tf`; los secretos existen en el Key Vault según el informe operativo de José del 2026-11-23 (no se puede comprobar desde el repositorio) |
| T-09 | `docs/endpoints.md` si cambia el esquema, `Memoria.md` e informe | — | parcial (`docs/rotacion-de-claves-de-servicio.md`; `docs/endpoints.md` no cambia) |

## Definición de terminado
- Los casos CA de `spec.md` tienen prueba con su ID; `python scripts/sdd_lint.py --trace 056` en OK.
- Gates de `AGENTS.md §5` en verde, par revisor, `security-reviewer` LIBERA y `Memoria.md` al día.
