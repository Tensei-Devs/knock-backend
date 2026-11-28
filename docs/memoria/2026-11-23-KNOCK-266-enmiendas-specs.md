# 2026-11-23 · KNOCK-266 · Enmiendas a specs 037, 039, 043, 044, 047, 049, 050, 053 y 062 (borrador sin firmar)

## Qué cambió
Solo documentación (specs y `task.md`). Cada spec lleva una «Nota de enmienda (2026-11-23, decisión de José)» con el estado «Borrador sin firmar»; la aprobación original no se toca y el texto anterior de cada criterio se conserva como historial.
- 050: la persona ajena a la visita recibe 404 (CA-08, 14, 20, 37 y 43); un id mal formado (CA-27) y un `permiso` que no es booleano (CA-44) responden 400.
- 047: la cuenta sin perfil de técnico recibe 404 `not_a_technician` (CA-13, y CA-19 como extensión por compartir código).
- 062: CA-10 documenta que el contrato publicado deja `url` requerida y `proposito` libre (por `oasdiff`). CA-07 no se enmienda.
- 049: el motivo del ajuste es de 10 a 300 caracteres (R-08, R-15 y §6) y la tabla criterio→prueba cita las pruebas de #352.
- 037 y 044: el despachador debe crear el aviso en `avisos` (037 T-10, 044 T-11) y `sesion_revocada` debe emitirse en cierre, cierre remoto, expulsión por límite y reuso del token de renovación (037 T-11).
- 039: el aviso «exportación lista» es de texto fijo y sin enlace; endpoint nuevo propuesto para consultar la exportación (039 T-15).
- 053: las rutas del panel necesitan política 429 por persona del panel (053 T-11).
- 043: límites 30/h, 10/h y 120/min documentados como propuestos (el código está en #355, sin fusionar).

## Por qué
Decisiones de José del 2026-11-23 a partir de las contradicciones entre spec y código halladas en la auditoría. Las pruebas siguen al código; las specs se alinean con él donde José lo decidió.

## A evitar
- Marcar estas enmiendas como firmadas o tocar las casillas de aprobación: las firma José.
- Implementar 037 T-10 y T-11, 039 T-15, 053 T-11 y 044 T-11 antes de esa firma.
- Dar por hecho el nombre `GET /api/v1/me/exportaciones/ultima`: es una propuesta.
