# KNOCK-60 — reglas puras y despachador de avisos detrás de IPushSender (spec 037 T-02 y T-04 parcial)

## Qué cambió
- `AvisoRules`: carga v1 cerrada (R-04), solo datos (R-05), clave de deduplicación (R-06) y ventana de 5 minutos de `mensaje_nuevo` (R-07).
- `DespachadorDeAvisos` con puertos `IPushSender`, `IEnviadosStore`, `IPresenceReader` e `IDeviceTokens`: interruptor, validación, presencia, ventana, deduplicación, 3 reintentos y borrado de identificador inválido.
- `Notificaciones:PushHabilitado` apagado por omisión y `NoOpPushSender` registrado; no hay credenciales de Firebase.

## A evitar
Registrar un `IPushSender` real sin secretos en el almacén de despliegue (R-10); poner texto libre o identificadores personales en la carga.

## Pendiente (B12c)
Implementaciones reales de `IEnviadosStore` (Mongo, índice único con TTL), `IPresenceReader` (Redis `chat:presencia:{sub}`), `IDeviceTokens` (sobre `DeviceStore`), lectura/entrega del buzón (`POST /api/v1/interno/avisos`), cierre de ventana, envío FCM y T-03 completo, T-05 a T-09. Bloqueo: credenciales FCM (José). Los eventos usan los nombres de la spec backend (`solicitud_cancelada`, `solicitud_vencida`); el frontend 024 usa `request_cancelled` y `request_expired`: conciliar.
