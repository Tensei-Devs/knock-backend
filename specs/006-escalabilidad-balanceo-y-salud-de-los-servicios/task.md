# Tareas 006 — Escalabilidad, balanceo y salud de los servicios

> **Estado:** hechas (T-01 a T-04) en `feature/KNOCK-201-scaling-and-health`, sin commit; pendientes la revisión de par y el PR.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Agregar la variable de réplicas y las sondas de salud | R-01, R-02, R-06 · CA-03 | hecha |
| T-02 | Configurar los encabezados reenviados confiables | R-03 | hecha |
| T-03 | Implementar el almacén compartido de tokens cancelados y contadores en Redis | R-04, R-05 · CA-02 | hecha (verificación; ver nota) |
| T-04 | Probar con dos copias y apagar una | R-01 · CA-01 | hecha |

## Notas
- **T-01:** `deploy/apps/variables.tf` fija min=max=2 por defecto en los cuatro servicios (R-01); el comentario explica cómo bajar a 1 fuera de la ventana (R-06). Redis queda en 1 (`main.tf`). Las sondas ya estaban: el módulo `deploy/modules/service` usa `/health` y `/health/ready` por defecto y `deploy/apps` no las sobreescribe (R-02). **No verificado con terraform** (no hay terraform en esta máquina): hay que correr `terraform fmt -check` y `terraform validate` (init `-backend=false`) donde exista.
- **T-02:** `Knock.Messaging.Api` registra `AddKnockForwardedHeaders()` y `UseForwardedHeaders()` igual que Identity/Market; `deploy/apps/main.tf` le pasa `ForwardedHeaders__KnownNetworks__0`. Pruebas `_R_006_03` en `tests/UnitTest/Hosting/Spec006ForwardedHeadersTests.cs` (configuración y suplantación ignorada desde IP no confiable, aceptada desde red confiable).
- **T-03 (verificación, sin reescribir):** sesiones canceladas, contadores del gateway y caché del perfil de Identity ya viven en Redis. **Contadores en memoria:** Identity y Market usan `PartitionedRateLimiter` en proceso; con 2 copias el límite se aplica por copia. La caché de Market aún no está implementada (solo declarada en `CatalogStore`, TT-BD-08). Decidir si se comparten en Redis queda pendiente de una persona.
- **T-04:** `tests/IntegrationTest/Identity/Spec006ScalingTests.cs` (`_CA_006_01`..`_CA_006_03`) con dos `IdentityApiFactory` compartiendo MongoDB y Redis de Testcontainers. CA-01/CA-02 requieren el emulador de Firebase Auth (`FIREBASE_AUTH_EMULATOR_HOST=127.0.0.1:9099`).

## Verificación
- `dotnet build Knock.slnx -c Release -warnaserror`: 0 errores, 0 advertencias.
- `dotnet format Knock.slnx --verify-no-changes`: sin cambios.
- `dotnet test tests/UnitTest`: 778/778. `tests/SecurityTest`: 286/286. Integración `Spec006`: 3/3 (Docker una vez, con emulador de Firebase).

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
