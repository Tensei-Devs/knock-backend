# 2026-11-23 · KNOCK-86 · Cola y decisión reales de las recuperaciones de acceso en el panel (spec 053, D-07)

## Qué cambió
`AccessRecoveryReviewStore` (módulo Moderation, sobre las colecciones `recuperaciones`, `usuarios` y `sesiones`) reemplaza el valor por omisión `NoAccessRecoveryReviews` del puerto `IAccessRecoveryReviews`; el aviso y la cancelación de sesiones tras confirmar pasan por el puerto nuevo `IRecoverySessionClosures`, que implementa `RecoverySessionClosures` en Account. La cola `GET /admin/verificaciones?tipo=recuperacion` lista los casos de `recuperaciones` por estado (`en_revision` = `pendiente`, `verificada` = `aprobada`, `no_verificada` = `rechazada`) y antigüedad, con cursor; el campo `nivel` lleva el tipo de cuenta del caso. La decisión aprobada cambia el celular protegido de la cuenta (ya cifrado y con HMAC, sin descifrarlo), revoca todas sus sesiones y deja `recuperacion_decidida` en la misma transacción; después de confirmar deja el aviso `sesion_revocada` y cancela las sesiones en Redis. Rechazar solo cierra el caso. Un celular que otra cuenta ya tiene responde 409 `phone_already_registered` sin cambiar nada (`RecoveryDecisionOutcome.PhoneTaken`). Las decisiones simultáneas se turnan con `DecisionTurns`. Sin contrato nuevo ni scripts de `db/`: los índices de `recuperaciones` ya existen. Pruebas: `Spec053RecuperacionesPanelTests`.

## Por qué
CA-32 y CA-36 de la 053 piden que aprobar una recuperación cambie el número y cierre las sesiones, y T-04 estaba parcial porque el puerto solo tenía el valor por omisión (la cola de recuperaciones salía vacía y toda decisión respondía 404).

## A evitar
- Descifrar o registrar el celular nuevo: pasa de un documento a otro tal cual.
- Cerrar las sesiones fuera de la transacción de la decisión.
- Usar `nivel` de la cola como nivel de verificación en una recuperación: ahí es el tipo de cuenta.

## Autorrevisión de seguridad (2026-11-24)
- Qué cambió: decidir la recuperación de la propia cuenta responde 403 `self_review_not_allowed` (D-05) sin efectos; el cierre posterior a la confirmación intenta todas las sesiones aunque una falle (aviso y Redis por separado) y, si algo falla, la petición responde error en lugar de éxito; un reintento de aprobación sobre un caso recién aprobado repite la cancelación en Redis de las sesiones revocadas (hasta 15 minutos desde la decisión) antes de responder 409.
- Por qué: si Redis fallaba tras confirmar, el caso quedaba aprobado, el reintento daba 409 y los tokens de acceso de las sesiones viejas seguían vivos hasta 15 minutos con el celular ya cambiado.
- A evitar: tratar la cancelación en Redis como opcional; riesgo aceptado: si nadie reintenta, el token de acceso vive como máximo 15 minutos (los tokens de renovación ya están revocados en la base).
