# Media: verificación de uso y enlaces de lectura de fotos privadas (spec 020b, KNOCK-185)

## Qué cambió
- `IMediaApi` suma `IsReadyOwnedAndUsedForAsync` (existe, es del dueño, está `listo` y tiene el `uso` pedido) y `CreateReadLinksAsync` (enlace de lectura de 5 minutos de la versión reducida de archivos `listo` con el `uso` pedido). El `uso` viaja como cadena (`solicitud` o `trabajo`) y el resultado como tupla, porque la frontera de módulo solo permite la interfaz y su clase de registro como tipos públicos.
- `MediaApi` emite los enlaces con `IEnlacesPrivados` sobre el contenedor `archivos`, que entra en la lista de contenedores enlazables; nunca la cuarentena ni la cuenta pública. Cada enlace deja un registro de acceso con los motivos nuevos `foto_solicitud` y `foto_trabajo` y la huella HMAC del `sub` de quien lo pidió.
- `MediaRequestPhotos` y `MediaVisitPhotos` comprueban el `uso` (`solicitud` y `trabajo`); `ReadLinksAsync` recibe el `sub` de quien ya está autorizado a ver la solicitud (dueño o técnico elegible) y devuelve los enlaces reales en el detalle y la bandeja. Si Media falla, la solicitud se muestra sin fotos y solo se registra el tipo de error.

## Por qué
La spec 020 (R-04, CA-18) exige rechazar fotos de otro `uso` y la spec 032 (R-16) pide enlaces privados de 5 minutos con registro; sin ello el detalle salía con `fotos: []` y una foto de perfil podía usarse en una solicitud.

## A evitar
- Exponer tipos públicos nuevos en el módulo Media: la prueba `ModuleSurfaceTests` lo impide.
- Enlazar la cuarentena, la cuenta pública o un contenedor distinto de `Media:FilesContainer`; `ContenedoresPermitidos` falla cerrado.
- Guardar o registrar el enlace firmado, el `sub` o el nombre del blob; el registro solo lleva motivo, identificador derivado del blob y huella.
- Pedir enlaces sin haber autorizado antes al lector: `CreateReadLinksAsync` no revisa dueño a propósito.
- Los motivos nuevos amplían la lista cerrada de la spec 032 R-16; falta reflejarlos en el texto de la spec.
