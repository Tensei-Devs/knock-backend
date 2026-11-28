# La baja de cuenta no anonimizaba opiniones y el técnico oculto no llegaba a Mercado

## Qué cambió
- Eliminar la cuenta marca `anonimizada` las opiniones que escribió esa persona (spec 039, R-25; spec 051, R-16). Lo implementa el módulo Reviews; se quitó el sustituto vacío.
- Identidad entrega `TecnicoOcultado` a `POST /api/v1/interno/tecnicos-ocultados` de Mercado con el permiso `tecnico-ocultado` (spec 053, R-20).

## Por qué
- Reproducido con pruebas antes de corregir: la opinión seguía con `anonimizada = false` y el evento quedaba siempre para reintento.
- La ruta de Mercado y la exportación de Mensajería ya existían; faltaba quien llamara a la primera. La exportación quedó resuelta aparte.

## A evitar
- Registrar un sustituto que no hace nada para un puerto obligatorio: el proceso responde bien sin haber hecho el trabajo.
- Probar solo el consumidor de un evento y no que alguien lo entregue.

## Pendiente
- Las cuentas que ya se hubieran eliminado y los eventos que hayan agotado sus reintentos no se corrigen solos; no se revisaron datos reales.
