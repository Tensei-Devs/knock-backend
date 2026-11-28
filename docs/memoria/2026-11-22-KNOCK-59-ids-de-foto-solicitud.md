# Spec 020: las fotos de la solicitud son archivoId de Media (KNOCK-59)

## Qué cambió

- `POST /api/v1/solicitudes` valida `fotos` como `archivoId` de 32 hexadecimales (UUID v7 sin guiones, spec 020 R-04 enmendado y spec 058 R-01); el `ObjectId` de 24 caracteres se rechaza con 422. El servicio guarda y devuelve la forma canónica en minúscula.
- `MediaRequestPhotos` adapta `IMediaApi` a `IRequestPhotos` y reemplaza a `UnavailableRequestPhotos` como registro por omisión. Comparte con `MediaVisitPhotos` la comprobación `MediaPhotoCheck` y el análisis del identificador `PhotoIds` (antes vivía en `VisitCloseRules`).
- Las semillas de demo de solicitudes usan marcadores de 32 hexadecimales y `DemoRequestSeeder` los valida con el mismo formato.
- Pruebas: `Spec020MediaRequestPhotosTests`, `Spec020FotosTests`, `Spec020ValidacionTests` y `Spec020RequestRulesUnitTests` con identificadores de archivo.

## Por qué

- Las fotos reales viven en Media con `archivoId` UUID v7; con el formato de 24 hexadecimales ninguna foto coincidiría y no se podría crear ninguna solicitud con el módulo real.
- Un solo comprobador evita dos copias del mismo código (el análisis de jscpd falla con cualquier clon).

## A evitar

- `IMediaApi` todavía no comprueba `uso = solicitud` ni emite enlaces de lectura (spec 032, OI-3): `ReadLinksAsync` devuelve una lista vacía y el detalle muestra `fotos: []`. No inventar direcciones ni exponer las del almacenamiento; se cierra cuando Media emita los enlaces.
- No buscar un valor corto de una prueba aleatoria dentro del cuerpo crudo de la respuesta.
