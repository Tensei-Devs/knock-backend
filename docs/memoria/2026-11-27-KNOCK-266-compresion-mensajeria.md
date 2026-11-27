# KNOCK-266 · Compresión en Mensajería y subidas fuera de la compresión en Mercado

## Qué cambió

- Mensajería usa la pieza compartida de compresión (spec 010, R-09), salvo todo lo que cuelga de `/hubs` y de `/api/v1/interno`.
- Mercado excluye, además de la autenticación, todo lo que cuelga de `/api/v1/imagenes/subidas`.

## Por qué

- Enmienda del 2026-11-27 a la spec 010. La spec 032 (R-15) no cambia: faltaba garantizarla en la confirmación.

## A evitar

- Probar la exclusión solo con la emisión del enlace: responde 201 y la compresión solo toca los 200, así que la prueba pasaría aun sin la exclusión.
- Comprimir en el gateway: no podría respetar las exclusiones de cada servicio.
