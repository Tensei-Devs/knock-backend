# 2026-11-23 · KNOCK-266 · Campos derivados en `VisitResponse`

## Qué cambió
`GET /api/v1/visitas/{id}` (y las vistas que reusan `VisitResponse`) suma `reprogramacionPendiente` (siempre presente; verdadero con una propuesta de la spec 050 cuyo `venceEn` no pasó), `estadoCotizacionFinal` (el estado guardado en `cotizacionFinal.estado`), `permisoFotos` (`permisoPublicarFotos`) y `terminadaEn`. Los opcionales se omiten si no hay dato.

## Por qué
La app necesitaba saber, al abrir la visita, si hay una reprogramación por responder, en qué va la cotización final y si el cliente permitió publicar fotos, sin rutas nuevas.

## A evitar
- Escribir al leer: la vigencia de la propuesta se calcula con la hora actual, el ejecutor de vencimientos sigue siendo quien la borra.
- Cambiar la semántica de los campos existentes: todo es aditivo.
- `estadoCotizacionFinal` es el estado guardado: una `enviada` vencida se ve `vencida` solo cuando el ejecutor la marca (la lectura calculada es la spec 063, borrador).
