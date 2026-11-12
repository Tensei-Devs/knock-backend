# Tareas 037 — Registro de dispositivos y despachador de avisos push

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.
> «Depende de» = tareas que deben estar fusionadas antes; «Paralelo con» = tareas que se pueden trabajar a la vez.

| Tarea | Qué se hace | Cubre | Depende de | Paralelo con | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-01 | Índices de `notifications.dispositivos` (persona + token único), `notifications.enviados` (dedupe por `evento`, `recurso` y persona) y `notifications.pendientes`, en `db/`, y prueba de base de datos | §6, R-06 | spec 003 fusionada | T-02 | Alexis | pendiente |
| T-02 | Reglas puras: carga v1 con lista cerrada (R-04), solo datos (R-05), clave de deduplicación (R-06) y ventana de 5 minutos de `mensaje_nuevo` (R-07) (unitarias y mutación acotada) | R-04 a R-07 · CA-03, CA-04, CA-05, CA-10 | — | T-03, T-04 | T-01 | Alexis |
| T-03 | `DispositivosController` y `DispositivosService`: registro idempotente por token (201/200), reasignación al iniciar sesión, `DELETE` que responde 204 siempre, cifrado del identificador | R-01, R-02, R-03 · CA-01, CA-02, CA-09, CA-13 | T-01 | T-04 | T-02 | Alexis (revisa Guillermo) |
| T-04 | `DespachadorDeAvisos`: lectura del buzón, deduplicación, presencia `chat:presencia:{sub}` (TTL 60 s), envío por FCM con reintentos y registro en `notifications.enviados` | R-06 a R-09 · CA-03, CA-05, CA-06, CA-07, CA-08, CA-11 | T-02 | T-03, T-05 | T-03 | Alexis |
| T-05 | Pruebas de integración (MongoDB y Redis reales, FCM simulado) y seguridad negativa: identificador ajeno, carga con campos extra, registros sin datos, token inválido | CA-02, CA-03, CA-06, CA-07, CA-08, CA-13 | T-03, T-04 | T-06, T-07 | Alexis (revisa Guillermo) | pendiente |
| T-06 | Contrato `openapi-diff` con ejemplos de carga v1 de frontend 024 (`app/test/fixtures/avisos/*.json`); arquitectura (el despachador solo lee el buzón); regresión de las specs 020, 021, 026 y 030 | CA-04, CA-10 · §7 Contrato, Arquitectura, Regresión | T-05 | T-07 | Alexis | pendiente |
| T-07 | Carga acotada (1 000 eventos a 200 personas, sin pérdidas ni duplicados) y humo en evaluación (`POST /api/v1/dispositivos` sin token responde 401) | CA-03 · §7 Carga y Humo | T-05 | T-06 | Alexis | pendiente |
| T-08 | Secretos de Firebase desde el almacén del despliegue (R-10), escaneo de secretos sin hallazgos y prueba de que no aparecen en registros; OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` | R-10 · CA-12 | T-03, T-04 | T-06, T-07 | Alexis | pendiente |
| T-09 | Pasar las puertas de CI (análisis, formato, pruebas, cobertura, escáneres, `oasdiff`) y actualizar la memoria (`Memoria.md`), con revisión de par | §7 | T-06, T-07, T-08 | — | Alexis (revisa Guillermo) | pendiente |

## Dependencias con otras specs
- **Spec 003** entrega el buzón de salida que el despachador lee.
- **Specs 026 y 030** fijan el grupo `usuario:{sub}` y la presencia para R-08.
- **Frontend 012 y 024** consumen el registro y la carga v1; su doble de API se usa mientras la spec 037 no esté fusionada.
- **Transporte entre servicios** (§8.1): si el buzón no alcanza el volumen, CA-03 de punta a punta espera a la spec de transporte.

## Ruta crítica y trabajo en paralelo
- Ruta crítica: T-01 y T-02 → T-04 → T-05 → T-06 → T-09.
- En paralelo: T-03 con T-04; T-07 y T-08 mientras T-06 corre.

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde (`python scripts/sdd_lint.py --trace 037` sin `FALTA`).
- El CI está en verde, sin hallazgos de secretos, y el contrato OpenAPI no tiene cambios incompatibles fuera de las dos rutas nuevas.
- Un par revisó el cambio y `Memoria.md` está al día.
