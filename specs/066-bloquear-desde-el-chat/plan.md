# Plan 066 — Bloquear desde una conversación

> Parte de `spec.md` (estado Borrador; no habilita código). Redactado el 2026-11-28.

## Enfoque
1. Resolver D-01 a D-03 y ajustar la spec antes de la firma.
2. Mensajería: ruta interna de contraparte con su permiso, pruebas primero.
3. Identidad: cliente interno y las dos rutas, reutilizando las reglas de bloqueo de la spec 043.
4. Contrato, matriz de límites, enmienda propuesta a la 043 y memoria.

## Archivos que se crearían o cambiarían
`src/Knock.Identity.Api/Modules/Safety/`, `src/Knock.Messaging.Api/Modules/Conversaciones/`, `tests/`, `docs/endpoints.md`, `docs/rate-limit-matrix.md` y una entrada en `docs/memoria/`. `docs/openapi.v1.json` es un archivo protegido: su cambio necesita autorización.

## Estimación sin medir
6 a 10 horas en el backend con las pruebas, sin contar el CI ni la revisión. En la app (enmienda o spec propia y pantalla), 2 a 4 horas más.
