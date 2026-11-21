# La CDN no se puede desplegar en Azure for Students (KNOCK-296)

## Qué cambió
El ADR-007, `docs/evaluacion/brechas-conocidas.md`, la fila 15c de `docs/CUMPLIMIENTO_LINEAMIENTOS.md` y `deploy/edge/README.md` dicen ahora que el módulo de Front Door quedó preparado y validado pero no se desplegó, con el mensaje de error literal de Azure.

## Por qué
Al aplicar, Azure respondió 400 BadRequest: Free Trial and Student account is forbidden for Azure Frontdoor resources. Después del fallo no quedó ningún recurso creado.

## A evitar
No presentar la CDN como implementada ni omitirla. No intentar alternativas ni cambiar de suscripción sin decisión del Tech Lead.
