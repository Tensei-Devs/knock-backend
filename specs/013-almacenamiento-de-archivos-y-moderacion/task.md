# Tareas 013 — Almacenamiento de archivos, CDN y moderación

> **Estado:** OI-1 implementada (contratos/estructura) · OI-2 y OI-3 pendientes.

| Tarea | Qué se hace | Cubre | Fase | Estado |
|---|---|---|---|---|
| T-01 | Crear los contenedores y la CDN en Terraform | R-01, R-04 | OI-3 | pendiente |
| T-02 | Implementar el servicio de enlaces firmados | R-02 · CA-02 | OI-3 | pendiente (OI-1 solo define la constante `SasLifetime` de 5 minutos, sin generar enlaces) |
| T-03 | Implementar la limpieza de metadatos y la reducción de tamaño | R-03 · CA-01 | OI-2 | pendiente (OI-1 solo incluye reglas puras de formato y dimensiones, sin procesamiento) |
| T-04 | Implementar el cliente de moderación compartido con falla cerrada | R-05, R-06, R-07 · CA-04, CA-05 | OI-1 (contrato) / OI-2 (falso) / OI-3 (real) | parcial: contrato `IContentModerator` con `Unavailable` como bloqueo en OI-1; implementación pendiente |
| T-05 | Escribir las pruebas de seguridad negativa | R-04 · CA-03 | OI-2 / OI-3 | pendiente (OI-1 solo incluye pruebas unitarias de validación y reglas) |

## Fases de implementación

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
- **Origen de 12 000 px por lado y 24 MP totales:** TT-API-05 legacy (`specs/_legacy/TT-API-05-servicio-de-fotos-privadas-blob-sas/spec.md`, donde `MaxPixels` baja de 40 a 24 MP, y `docs/migracion/Memoria_origen.md`, decisión de Guillermo del 2026-10-23, punto 2; `MediaRules.MaxPixels` / `MediaRules.MaxSidePixels` en `src/Knock.Market.Api/Modules/Media/Domain/MediaRules.cs`). En OI-1 se reutilizan esos límites de TT-API-05 para rechazar antes de decodificar.

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.

  Aclaración: esta definición aplica a la finalización integral de la spec 013. El cierre de cada fase OI-1/OI-2/OI-3 se evalúa contra su alcance y entregables asignados. OI-1 no declara cubiertos los criterios de aceptación completos que están asignados a fases posteriores.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
