# KNOCK-266 · HSTS opcional en el ambiente de desarrollo

## Qué cambió

- El gateway tiene `Security:Hsts:SendInDevelopment`, apagada por omisión. Encendida, envía `Strict-Transport-Security` también en Development, salvo a `localhost`, `127.0.0.1` o `::1` (BUG-002, enmienda del 2026-11-27).

## Por qué

- El ambiente de desarrollo desplegado usa el entorno Development y se atiende por HTTPS; pasarlo a Production hoy dejaría el chat sin envío de mensajes.

## A evitar

- Encenderla en la configuración local: el navegador recuerda el encabezado hasta un año.
- Dar por activo el encabezado en el ambiente desplegado: falta que Terraform defina la variable en el perfil de desarrollo y que una persona la aplique.
