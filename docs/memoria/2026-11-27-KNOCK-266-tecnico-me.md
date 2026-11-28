# KNOCK-266 · `GET /api/v1/tecnicos/me`

## Qué cambió

- Nueva lectura del perfil propio (spec 046, enmienda del 2026-11-27, TEC-15): los campos del perfil más `fotoUrl` y `direccionTaller`.
- La ruta `mercado-tecnico-me` del gateway acepta `GET` además de `PUT`.
- El contrato (`docs/openapi.v1.json` y el público) se editó a mano porque no se compiló en local; el CI lo regenera y compara.

## Por qué

- La app solo tenía `PUT /tecnicos/me`: no podía precargar la edición ni conocer su identificador de técnico.

## A evitar

- Dejar el `GET` fuera de la ruta del gateway: caería en la ruta anónima `/api/v1/tecnicos/{id}`.
- Reutilizar esta respuesta en rutas públicas o guardarla en caché: lleva la dirección del taller descifrada.
