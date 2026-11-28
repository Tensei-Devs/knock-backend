# 053: decisiones simultáneas de reporte y verificación responden 409, no 500 (G15)

## Qué cambió

- `DecisionTurns` turna, dentro de la instancia, las decisiones sobre un mismo reporte o verificación (candado por franja del hash del id). Ya dentro del turno se relee el estado: si ya no está pendiente se responde «ya decidido» (409) o «no existe» (404) sin abrir transacción.
- `ReportReviewStore.DecideAsync` y `VerificationReviewStore.DecideAsync` usan ese turno; la transacción existente quedó en `DecideInTransactionAsync`.
- La prueba CA-053-27 ya no acepta `MongoCommandException` como perdedor válido: exige un solo 2xx y que los otros nueve reciban 409.

## Por qué

- Diez transacciones sobre el mismo documento se bloquean entre sí en documentdb-local y agotan el tiempo, lo que salía como 500. Es la misma causa raíz y la misma solución que KNOCK-295.

## A evitar

- No volver a tratar el error de bloqueo como perdedor válido en las pruebas ni subir reintentos para taparlo.
- El turno es por instancia; entre varias instancias sigue valiendo el filtro por estado de la transacción.
