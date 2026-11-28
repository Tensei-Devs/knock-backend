# Plazo de vencimiento configurable de la solicitud (KNOCK-71, spec 020 T-15)

## Qué cambió
`Solicitudes:HorasVencimiento` (1 a 168 horas, 48 por omisión) pasa a ser la fuente de `venceEn` al crear una solicitud, en lugar de las 48 h fijas. Se valida al arrancar (`SolicitudesOptions`) y `SolicitudService` lo recibe por opciones.

## Por qué
La spec 029 (R-08) pide un plazo configurable; la spec 020 T-15 es el ajuste que lo conecta con la creación de la solicitud.

## A evitar
No volver a las 48 h fijas en `RequestRules`; no aceptar valores fuera de 1 a 168 (el arranque debe fallar).
