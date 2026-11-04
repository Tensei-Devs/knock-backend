# Pendientes y decisiones de la migración (backend)

Foto del 2026-11-03, rama `migracion`. Nada de esto se ha mezclado en `main`.

## Código que quedó en `legacy/` (por migrar)
| Módulo o pieza | Por qué espera | Servicio destino |
|---|---|---|
| Identity (login, sesión, perfil, `FirebaseSessionService`, `SessionTokenIssuer`) | Decisión abierta: `refresh_tokens` frente a `sesiones`, `jti` frente a `sid`, `deviceId` (specs 015-018 frente a HU-001 y TT-API-12) | `Knock.Identity.Api` |
| Trust | Depende de Identity | `Knock.Identity.Api` |
| Messaging, Notifications | Sin pruebas propias; esperan sus specs | `Knock.Messaging.Api` |
| Requests, Reputation, Assistant, Media | Esperan sus specs y el reparto Market / Asistente | `Knock.Market.Api` y un servicio de Asistente (sin proyecto todavía) |
| `tests/IntegrationTest` y `tests/SecurityTest` | Sus fixtures (`TestHttp`, `FirebaseEmulator`, `MediaHttp`) emiten tokens con el módulo Identity | Se portan junto con Identity |
| `tests/Spike.DocumentDb` | Spike de TT-BD-05, solo histórico | — |

## Verificado en esta rama
- `Knock.slnx` compila en Release con `-warnaserror` (net11.0, SDK 11.0.100-rc.3), sin advertencias.
- `tests/UnitTest`: 257 pruebas en verde. `tests/DatabaseTest`: 35 en verde (con `documentdb-local` en Docker).
- `sdd_lint`, `sync_agents --check`, `codemap --check`, `check_exceptions` y `pin_actions --check`: en verde.
- **No verificado:** los workflows de GitHub Actions (no se pueden ejecutar fuera de GitHub), los contenedores (no hay Dockerfile de Gateway, Identity ni Messaging) y `tests/IntegrationTest` y `tests/SecurityTest` (vacíos).

## Excepción nueva
- **EXC-006** (`security/exceptions.yaml`): `IStartupValidator` está obsoleto en .NET 11; vence 2026-12-02.

## Constitución: propuesta de fusión (la decide una persona)
Demo Base (1.1.0) sigue la estructura del profesor; el origen (2.2.x) añade reglas que Demo Base no tiene. Para fusionar sin perder reglas:

| Sección del origen | Qué hacer |
|---|---|
| §2 Principios no negociables | Comparar con §2 de Demo Base y traer los que falten |
| §3 Stack obligatorio | Mantener .NET 11 y los servicios de Demo Base; traer versiones fijadas de paquetes |
| §4 Arquitectura | Ya cubierta por las specs 001-002; conservar la regla de fronteras entre módulos dentro de cada servicio |
| §6 Seguridad y privacidad | Traer completa (datos sensibles, INE y selfie, cifrado del celular, alojamiento en EE. UU.) |
| §7 Pruebas y calidad, §8 Excepciones | Fusionar con §8 de Demo Base; traer la regla de excepciones con vencimiento |
| §9 Flujo, §10 Agentes, §11 Gobernanza | Mantener las de Demo Base y anotar las diferencias |
| §12 Rendimiento y caché, §13 Nube y costo, §14 App móvil y landing | Traer; hoy Demo Base solo tiene §5 de infraestructura |

El texto original está en `docs/migracion/constitution_origen.md`.

## Otras tareas abiertas
1. **Specs sin firma:** ver la tabla en `docs/migracion/README.md`. Un agente no las firma.
2. **PR abiertos del origen** (no están en `sprint-02`, no se migraron): backend #75, #86, #100, #102-#106, #108-#110; frontend #53, #54. Se migran cuando se fusionen.
3. **Workflow:** adaptar el job `container` para construir Gateway, Identity y Market (hoy apunta a `src/Knock.Gateway/Dockerfile`, que no existe) y quitar el job de DAST, que usa pila de PostgreSQL.
4. **Rulesets:** al probar el CI, llenar las comprobaciones obligatorias de `main` con los nombres de los jobs.
5. **Validador de specs `NNN-tema`:** `sdd_lint.py` solo cubre el formato TT/HU.
6. **Jira y ramas `KNOCK-n`:** se retoman al cerrar la migración.
