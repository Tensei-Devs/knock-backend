# Spec 055 T-17: CORS y límite de cuerpo en Knock.Hosting

## Qué cambió
- `Knock.Hosting.Http.CorsMiddleware` (con `KnockCorsOptions` y `UseKnockCors`) y `RequestBodyLimitMiddleware` (con `RequestBodyLimitOptions`) reemplazan las copias de Identidad y Mercado, que se borran.
- Lo que cambiaba entre copias queda como opciones (R-05): los encabezados de petición que concede cada servicio (Mercado agrega `If-None-Match` y `Cache-Control`) y el `code` del 413 (`payload_too_large` en Mercado; Identidad no lo escribe, como antes). Los orígenes y la vigencia del preflight siguen saliendo de `Security:Cors` de cada servicio.
- `HostingLibraryBoundaryTests.Servicios_Sin_Copias_Del_Catalogo_CA_055_12` ahora también prohíbe `CorsMiddleware` y `RequestBodyLimitMiddleware` en los servicios.

## Por qué
- Spec 055 R-08 y R-09: una pieza del catálogo se mueve completa. Sin cambio de comportamiento: mismas listas, misma respuesta 204 del preflight y mismo 413.

## A evitar
- No volver a copiar el middleware a un servicio; un encabezado nuevo se agrega en las opciones del servicio.
- La política CORS del gateway (`CorsExtensions`, con `UseCors`) sigue aparte y con EXC-016: usa otro mecanismo y otra lista de encabezados; moverla pide su propio PR.
- Mensajería no usa CORS ni este middleware: su tope de cuerpo es el atributo `BodyLimit`.
