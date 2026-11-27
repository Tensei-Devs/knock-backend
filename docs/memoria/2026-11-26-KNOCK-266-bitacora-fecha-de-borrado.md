# La bitácora guardada antes del cierre no se borraba

## Qué cambió
- Una revisión periódica del módulo Assistant (`FechadoDeBitacoras`, cada 15 minutos) pone `venceEn` (cierre más 30 días) a las bitácoras sin fecha cuya solicitud ya se cerró; el índice TTL existente las borra (spec 052, R-21 y CA-34).
- `IRequestsApi.GetClosedAtAsync` responde cuándo se cerró cada solicitud, participe o no el técnico.

## Por qué
- `venceEn` solo se calculaba al guardar: con la solicitud abierta quedaba en `null` para siempre y el TTL no la tocaba.
- Se revisa en lugar de escribirlo en cada cierre porque hay varias vías de cierre y así Requests no escribe en colecciones de Assistant. Al cancelar o vencer, el envío queda `cerrado`, así que la consulta de participación ya no devuelve la fecha.
- No es un tipo de `tareas_programadas`: ese catálogo es cerrado (spec 054, R-08).

## A evitar
- Calcular una fecha de borrado solo al escribir el documento cuando el hecho que la fija puede ocurrir después.

## Pendiente
- Una solicitud con cita solo se cierra cuando algo la pasa a `terminada` o `cancelada`; hoy en `sprint-05` eso no ocurre al cerrar la visita (lo agrega el PR #413, sin fusionar). Hasta entonces su bitácora sigue sin fecha.
