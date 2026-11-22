# Avisos dentro de la app y preferencias de avisos (KNOCK-105)

## Qué cambió
Mensajería (`Knock.Messaging.Api`) tiene el módulo `Notices` con las rutas de lectura de la spec 044: `GET /api/v1/avisos` (lista por cursor con filtros `leido` y `comoRol`), `GET /api/v1/avisos/no-leidos/contador` (tope 99), `POST /api/v1/avisos/{id}/leido`, `POST /api/v1/avisos/leidos` y `GET /api/v1/me/preferencias-avisos`. El script `db/044-avisos-dentro-de-la-app.js` crea `avisos` (índice por persona, lectura y fecha, y TTL en `venceEn`) y `preferencias_avisos` (una por persona). Las políticas `avisos-leer` (120 por minuto) y `avisos-marcar` (60 por minuto) limitan por `sub`; las dos rutas `POST` responden 413 con un cuerpo mayor de 1 KiB. El gateway publica `/api/v1/avisos/**` y `GET /api/v1/me/preferencias-avisos` hacia Mensajería.

## Por qué
La app necesita la campana y la bandeja (HU-051). Toda consulta y escritura filtra por el `sub` del token y un aviso ajeno responde 404 `notice_not_found` igual que uno inexistente, para no revelar que existe.

## A evitar
Crear avisos desde este módulo: solo los crea el despachador de la spec 037 (hoy sin implementar), con `venceEn` calculado con `NoticeRules.ExpiresAt` (30 días). Cambiar las preferencias aquí: el PUT lo define la spec 037. Registrar títulos, textos o destinos de avisos: el módulo no escribe registros y una prueba obliga a revisar cualquier cambio.

## Decidido
- CA-09 pide 403 y R-12, CA-11 y CA-18 piden 404; se aplica 404 uniforme porque cualquier token válido es una persona y un 403 distinto diría que el aviso existe.
- Sin `leido` la lista trae leídos y no leídos (HU-051 pide historial con no leídos destacados); el «por defecto falso» de R-02 se toma como el valor guardado del campo.
- Lista cerrada de tipos de aviso: `cotizaciones`, `mensajes`, `visitas`, `solicitudes`, `opiniones`, `verificaciones`, `seguridad` y `promociones`.
- `usuarioId` se guarda como el `sub` del token (texto), porque Mensajería no lee la colección `usuarios` de Identidad; la spec dice ObjectId.

## PARA VALIDACIÓN DE JOSÉ
Las cuatro decisiones de arriba y el formato de `usuarioId`. Alternativa descartada para el 403: devolverlo cuando el token no tenga rol de cliente ni de técnico (el token de Knock no lleva ese dato en Mensajería).