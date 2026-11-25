# Spec 055 T-17: un solo ProblemResponses en Knock.Hosting

## Qué cambió
- `Knock.Hosting.Http.ProblemResponses` trae `AddKnockProblemDetails(KnockProblemDetailsOptions?)` y `WriteAsync(context, status, title, code?, extensions?)`. Se borraron las cuatro copias: `ProblemResponses` del gateway y de Mensajería, `ProblemDetailsExtensions` de Identidad y Mercado, y la clase `ProblemResponses` que vivía dentro de `RequestBodyLimitMiddleware.cs` de Identidad y Mercado.
- Lo que cambiaba entre copias queda como opciones (R-05): `W3CTraceId` (Identidad lo apaga: deja el `traceId` por defecto de ASP.NET Core, el `Activity.Id` completo, en vez del identificador W3C de 32 hexadecimales) y `ExposeExceptionDetail` (el gateway lo apaga: nunca escribe el detalle). El comportamiento de cada servicio no cambia.
- El manejador de excepciones de dominio de Identidad y Mercado ya no lo registra `AddKnockProblemDetails`: lo registra cada servicio en su `KnockApplicationExtensions` (la biblioteca no conoce el dominio).
- El identificador de correlación se lee de `HttpContext.Items` con la clave `Knock.CorrelationId`, porque `Knock.Hosting` no puede referenciar `Knock.Contracts` (R-02); `Spec055ProblemResponsesTests` comprueba que la clave es la de `CorrelationId.ItemKey`.
- `HostingLibraryBoundaryTests.Servicios_Sin_Copias_Del_Catalogo_CA_055_12` impide que vuelvan estas piezas a un servicio.

## Por qué
- Spec 055 R-08 y R-09: una pieza del catálogo se mueve completa y una copia nueva no se acepta. Es el primer paso para retirar EXC-016 y la parte de EXC-021 y EXC-015.

## A evitar
- No volver a poner un `ProblemResponses` en un servicio; si hace falta otro campo, es una opción de `KnockProblemDetailsOptions`.
- No cambiar el comportamiento al mover (por ejemplo, igualar el `traceId` de Identidad al de los demás) en este PR: sería otra spec.
- Los marcadores `jscpd:ignore` de EXC-016 en `ProblemResponses` del gateway y de EXC-021 en el de Mensajería se fueron con los archivos; los de `CorsExtensions` y `AuthenticationExtensions` siguen.
