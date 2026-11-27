# El 403 de descartar y de la bandeja salía sin el formato de error completo

## Qué cambió
- `DescarteController` y `BandejaController` usan `BusinessRuleController.Rule`, que arma el ProblemDetails con `title`, `traceId`, `correlationId` y `code`.

## Por qué
- Lo destapó una prueba de seguridad por ruta: el 403 `not_a_technician` de descartar salía sin `title` (spec 002, R-03; spec 028, R-17). El 409 usaba el mismo método. No era una fuga de autorización.

## A evitar
- Construir `new ProblemDetails` a mano en un controller: no pasa por la fábrica y pierde campos obligatorios.

## Pendiente
- `CotizacionesController` también arma su ProblemDetails a mano; no se comprobó si le faltan campos.
