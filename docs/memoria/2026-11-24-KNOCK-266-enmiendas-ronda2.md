# 2026-11-24 · KNOCK-266 · Segunda ronda de enmiendas a specs 037, 053, 055, 056, 058, 063 y 064 (borrador sin firmar)

## Qué cambió
Solo documentación (specs y `task.md`). Cada spec enmendada lleva una «Nota de enmienda (2026-11-24, decisión de José)» con el estado «Borrador sin firmar»; la aprobación original no se toca y el texto anterior de cada criterio se conserva en la marca inline.
- 037 (R-11): el evento se acepta siempre; el aviso dentro de la app (`avisos`) se crea aunque no haya dispositivo y el push solo se intenta si los hay. T-10 se amplía; 044 T-11 verifica el caso.
- 053 (R-25, CA-39): límites del panel por sesión del panel (`sid`): 120 por minuto en lecturas y 30 por minuto al abrir y decidir, con 429 `Retry-After` y 413 por tamaño. T-11 con los valores.
- 055 (R-06): los tipos de opciones de Redis y MongoDB son `public` para `DatabaseTest`, sin `InternalsVisibleTo` (ya fusionado en #362 y #365). EXC-018 sigue acotada al método `Matches`; no se enmienda R-04.
- 056 (CA-03): el código del 403 es `service_not_allowed` en Mercado y Mensajería e `internal_token_not_allowed` en Identidad.
- 058 (CA-15): el migrador reporta cantidad y motivo de los fallidos, sin nombres de blob; el código hoy registra el nombre del JSON antiguo, tarea nueva T-10.
- 063 y 064 (R-06, CA-08, CA-06, OpenAPI): un `id` de visita mal formado responde 400, no 422; tareas de código 063 T-07 y 064 T-07.
- 062: sin cambio de spec; `task.md` T-09 anota que el código debe responder `moderation_unavailable` (CA-07).
- Limpieza de las notas firmadas en #370: «depende de esa firma» pasa a «desbloqueada por la firma del 2026-11-24» (037, 039, 043, 044, 053 y sus `task.md`); 049 ya no dice «pendiente de fusión»; 047 CA-19 sin «extensión a confirmar».

## Por qué
Decisiones de José del 2026-11-24 para resolver contradicciones entre spec y código halladas en la auditoría. Las pruebas siguen al código; las specs se alinean con él donde José lo decidió, y donde el código contradice una spec ya firmada (062 CA-07) solo se anota la tarea.

## A evitar
- Marcar estas enmiendas como firmadas o tocar las casillas de aprobación: las firma José.
- Implementar 037 T-10, 053 T-11, 058 T-10, 063 T-07 y 064 T-07 antes de esa firma.
- Dar por hecho que el aviso dentro de la app depende del dispositivo, o dar a `Knock.DatabaseTest` un `InternalsVisibleTo` suelto.
