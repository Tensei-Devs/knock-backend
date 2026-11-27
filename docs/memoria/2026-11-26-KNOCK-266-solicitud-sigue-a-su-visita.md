# La solicitud sigue a su visita

## Qué cambió
- La solicitud en `cita_confirmada` pasa a `terminada` en la misma transacción en que su visita termina (al terminarla, o al rechazarse o vencer la cotización final).
- Pasa a `cancelada` con `visit_cancelled` si la visita se cancela, y con `visit_no_show` si cualquiera de las dos partes no se presenta. La cotización aceptada no cambia.
- El técnico elegido sigue viendo el detalle (sin dirección) de una solicitud cancelada por su visita; los demás reciben 404 (spec 029, R-13 y CA-10).

## Por qué
- Enmienda firmada del 2026-11-26 de las specs 020 y 029. Antes ninguna solicitud llegaba a `terminada` ni, por tanto, a `calificada`.

## A evitar
- Agregar una forma nueva de cerrar una visita sin actualizar la solicitud con `SolicitudDeLaVisita` dentro de la misma transacción.
- Aceptar `visit_cancelled` o `visit_no_show` como entrada de la cancelación del cliente.

## Pendiente
- No se corrigen las solicitudes cuyas visitas se cerraron antes de este cambio.
