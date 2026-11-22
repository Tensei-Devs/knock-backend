# Bloqueos: bandeja de salida real y proyecciones en Mercado y Mensajería (spec 043 T-08)

## Qué cambió
`BloqueosStore` guarda el bloqueo y deja `BloqueoCreado` o `BloqueoEliminado` en la bandeja de salida de Identidad, uno por destino (market y messaging), en la misma transacción; se retiró el puerto `IBlockEvents` y su sustituto. Mercado y Mensajería reciben el evento en `POST /api/v1/interno/bloqueos` (token de servicio, fuera del gateway) y mantienen la proyección `bloqueos`, idempotente y a prueba de desorden por la fecha del evento.

## Por qué
R-23 y CA-33: aplicar el bloqueo sin leer la base de Identidad (spec 001 R-03) y sin perder el evento si algo falla entre guardar y publicar.

## A evitar
Publicar el evento fuera de la transacción del bloqueo; aplicar un evento viejo sobre uno más reciente; incluir nombres, teléfono o texto en la carga; enrutar la ruta interna por el gateway.
