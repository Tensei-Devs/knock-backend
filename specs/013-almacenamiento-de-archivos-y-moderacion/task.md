# Tareas 013 — Almacenamiento de archivos, entrega con caché y moderación

> **Estado:** OI-1 implementada (contratos/estructura) · OI-2 y OI-3 pendientes.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Crear las dos cuentas de almacenamiento y sus contenedores en Terraform, sin CDN | R-01, R-04 | pendiente |
| T-02 | Implementar el servicio de enlaces firmados | R-02 · CA-02 | pendiente |
| T-03 | Implementar la validación (5 MB, JPEG/PNG/WebP), la limpieza de metadatos y la reducción de tamaño | R-03 · CA-01, CA-06 | parcial: OI-1 valida 5 MB y JPEG/PNG/WebP; la limpieza y la reducción van en OI-2 |
| T-04 | Implementar el cliente de moderación compartido con falla cerrada y 503 reintentable | R-05, R-06, R-07 · CA-04, CA-05 | parcial: OI-1 deja el contrato `IContentModerator` con `Unavailable` como bloqueo |
| T-05 | Escribir las pruebas de seguridad negativa (incluida la petición anónima a la cuenta privada) | R-04 · CA-03 | pendiente |
| T-06 | Crear el índice TTL de `subidas_imagen` (vence solo lo pendiente o rechazado) | R-08 · CA-07 | pendiente |

## Avance de la implementación (KNOCK-208)

### Fases

| Fase | Alcance | Estado |
|---|---|---|
| **OI-1** | Contratos (`IContentModerator` con falla cerrada), estructura `Media`, dominio/reglas puras, validaciones tipo/tamaño, `MediaOptions`, `MediaModuleExtensions`, integración en `ModulesExtensions`, pruebas unitarias y arquitectura | **Hecha** |
| **OI-2** | Procesamiento de imágenes y orquestación local con moderador falso (sin Azure, sin SAS, sin endpoints) | pendiente |
| **OI-3** | Blob Storage, User Delegation SAS, endpoints `archivos/*`, OpenAPI, Azurite/RBAC, moderación real con falla cerrada | pendiente |

## Criterios pendientes por fase
- **CA-01** (limpieza de metadatos): OI-2. En OI-1 no hay procesamiento de imágenes.
- **CA-02** (enlace vencido a los 5 minutos): OI-3. En OI-1 solo existe la constante `SasLifetime`, sin servicio de enlaces.
- **CA-03** (imagen privada por la CDN): OI-3 (T-05).
- **CA-04** (contenido inapropiado rechazado): OI-2 con moderador falso; la moderación real llega en OI-3.
- **CA-05** (moderación caída, no se publica): el contrato con `Unavailable` como bloqueo es OI-1; el comportamiento completo es OI-2/OI-3.

Las pruebas de OI-1 se nombran `*_OI1_NN` y no `*_CA_013_NN`, porque ninguna cubre todavía un criterio de aceptación completo.

## Notas de revisión (OI-1)
- **Excepción de «Media» en `ModuleSurfaceTests`:** la rama de `tests/UnitTest/Architecture/ModuleSurfaceTests.cs` que acepta una sola clase pública (`MediaModuleExtensions`) se quita cuando OI-2 agregue la interfaz del módulo (el test vuelve a exigir las dos superficies: clase de registro e interfaz). OI-1 no expone interfaz porque todavía no hay consumidor interno real del módulo.
- **Límites de las fotos:** 5 MB, 40 MP y 8 000 px por lado, en JPEG, PNG o WebP (decisión del Tech Lead, 2026-11-12; `MediaRules` en `src/Knock.Market.Api/Modules/Media/Domain/MediaRules.cs`). Reemplazan los 12 000 px y 24 MP de TT-API-05 legacy. Se rechaza antes de decodificar. La réplica de Market queda en 1 Gi con `MaxConcurrentProcessing=1` (se aplica en #63).

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.

  Aclaración: esta definición aplica a la finalización integral de la spec 013. El cierre de cada fase OI-1/OI-2/OI-3 se evalúa contra su alcance y entregables asignados. OI-1 no declara cubiertos los criterios de aceptación completos que están asignados a fases posteriores.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
