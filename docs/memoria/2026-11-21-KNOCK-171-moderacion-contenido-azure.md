# Moderación de contenido con Azure Content Safety: contrato, lista local, cliente, caché y topes (spec 031, T-01 a T-07)

## Qué cambió
- `Knock.Contracts/Moderation/ContentModeration.cs`: `IContentModeration` con `ModerateTextAsync` y `ModerateImageAsync`, `ModerationVerdict` (decisión, categorías, origen, motivo) y códigos de motivo. El `IContentModerator` de la spec 013 se conserva y un adaptador (`LegacyContentModeratorAdapter`, `sub` del token) lo cubre, de modo que los llamadores actuales reciben la decisión real sin cambios.
- Módulo `Modules/Moderation`: `NormalizadorDeModeracion`, `ListaLocal` (recurso incrustado `lista-local.es-MX.txt`, validada al arrancar, solo puede rechazar), `ContentSafetyClient` (REST, 5 s, un reintento, cortacircuito de 30 s tras 5 fallos), `ContentSafetyModerator` (longitud, lista, filtro de datos personales de la spec 024, caché, topes, registro), `ConsumoModeracionStore` (Redis, script atómico) y `UsoModeracionStore`, `PlantillasDeModeracion` y `ModerationProblemMapper`, `ModerationOptions` con validación (umbrales 2/4/6 y versión con huella).
- `db/031-moderacion-uso-moderacion.js`: `uso_moderacion` con TTL de 365 días en `market` y `messaging`.
- `Moderacion:Habilitado` apagado por omisión: sin él solo rechaza la lista local y el resto es `Unavailable` (falla cerrado), sin Redis, MongoDB ni Azure.

## Por qué
La spec 031 pide un solo punto de moderación que falle cerrado, sin datos personales hacia Azure y sin rebasar la cuota gratuita. El gasto en Azure es decisión del líder, por eso el interruptor.

## A evitar
- Aprobar sin respuesta válida de Azure o de la caché. - Llamar a Azure sin poder contar la llamada (Redis caído). - Subir un umbral sin cambiar `VersionUmbrales`. - Nombrar un archivo de recurso `*.es-MX.*` sin `WithCulture="false"`: MSBuild lo trata como satélite de cultura.

## Decidido
- Módulo propio `Modules/Moderation` en lugar de `Common/Moderation`: la regla de arquitectura exige Redis y Mongo solo en `Modules/*/Data`.
- Sin `Azure.Identity` por ahora: el cliente acepta un `TokenCredential` inyectado o una clave de Key Vault; sin credencial falla cerrado.

## PARA VALIDACIÓN DE JOSÉ
- Lista local inicial de 14 términos en español de México (insultos comunes): revisar y ampliar.
- Umbral `LlamadasPorMes` provisional en 4000 hasta confirmar la cuota del plan gratuito.
- Pendiente: copia en Mensajería (T-08) y uso en las rutas (T-09).