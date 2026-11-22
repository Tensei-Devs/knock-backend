# Cancelar solicitud y detalle de cerradas (KNOCK-70)

## Qué cambió
POST /api/v1/solicitudes/{id}/cancelar con cuerpo opcional, validador de motivo, límite de 10 por hora, reintento ante carrera y respuesta idempotente. El detalle agrega puedeVolverAPedir (solo vista del cliente) y el técnico recibe 404 si la solicitud está cancelada o vencida.

## Por qué
Spec 029 T-03 y T-05 (HU-027): cerrar solicitudes sin cita y permitir volver a pedir.

## A evitar
No reabrir solicitudes cerradas; no registrar el motivo; no exponer cerradas a técnicos.