# KNOCK-60 — cliente FCM real detrás de `Notificaciones:PushHabilitado` (spec 037 R-05 y R-10)

## Qué cambió
- `FcmPushSender` (FCM HTTP v1, mensaje solo de datos con prioridad alta), `FcmAccessToken` (JWT RS256 de la cuenta de servicio intercambiado en `token_uri`, guardado hasta un minuto antes de vencer) y `FcmOptions` (lee `Notificaciones:Fcm:ProjectId` y `Notificaciones:Fcm:ServiceAccount`).
- `AddPushSender`: con `Notificaciones:PushHabilitado=false` (por omisión) sigue `NoOpPushSender`, sin leer nada de FCM; encendido, falta de proyecto, cuenta ausente o mal formada impide arrancar (falla cerrado, el mensaje no repite el secreto).
- Terraform de Mensajería: `Notificaciones__PushHabilitado=false`, `Notificaciones__Fcm__ProjectId=knock-0` y el secreto `fcm-service-account` por `secret_env`.

## Por qué
El envío real debe existir listo y apagado para encenderlo tras el humo sin tocar código. Un token inválido (404 o UNREGISTERED) se reporta como `InvalidToken` para que el despachador lo borre.

## A evitar
Registrar el token del dispositivo o la cuenta de servicio en logs; encender el interruptor sin el secreto; usar mensajes con bloque `notification` (R-05 pide solo datos).

## Dependencias
Ninguna nueva: la firma usa `Microsoft.IdentityModel.JsonWebTokens`, ya presente por JwtBearer.
