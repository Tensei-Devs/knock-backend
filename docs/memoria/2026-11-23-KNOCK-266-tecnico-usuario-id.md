# 2026-11-23 · KNOCK-266 · `tecnicoUsuarioId` en las respuestas de Mercado

## Qué cambió
- `tecnicoId` (`_id` de `tecnicos`, ObjectId) y el `sub` del usuario (`usuarioId`) **no coinciden** en el modelo, y Mensajería identifica a los participantes por `sub`. Se añadió el campo opcional `tecnicoUsuarioId` (el `sub` del técnico; se omite si falta) a: cada cotización de `GET /solicitudes/{id}/cotizaciones`, la cita de `POST /solicitudes/{id}/elegir`, `VisitResponse` en la vista del cliente (`GET /visitas/{id}`), cada técnico de `GET`/`PUT /me/tecnicos-confianza` y el perfil `GET /tecnicos/{id}` solo con sesión. `PermisoConversacion` ya lo traía (spec 030).
- Los campos son opcionales al final de cada registro; no cambia ninguna semántica existente.

## Por qué
La app necesita el `sub` del técnico para abrir y reconocer conversaciones sin un viaje extra; con solo `tecnicoId` no podía.

## A evitar
- Publicar `usuarioId` en listas, explorar o mapa: son públicos y con caché compartida, y ligarían un perfil con una cuenta sin sesión. Por eso quedan fuera a propósito.
- Confundir `tecnico.id` (`tecnicoId`) con `tecnicoUsuarioId` al armar el chat.
