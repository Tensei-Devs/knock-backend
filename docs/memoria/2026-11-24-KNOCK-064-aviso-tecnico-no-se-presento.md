# 2026-11-24 · KNOCK-064 · Aviso al técnico que no se presentó

## Qué cambió
Mercado entrega `TecnicoNoSePresento` desde su bandeja a `POST /api/v1/interno/avisos` de Mensajería (`EntregaTecnicoNoSePresento`, mismo cliente, dirección y clave que `opinion_oculta`). Mensajería suma el tipo `tecnico_no_se_presento` (categoría `visitas`, rol `tecnico`) y la constante del texto fijo decidido por José (D-01). Enmienda fechada en la spec 064, `docs/events.md` y tarea T-04 cerrada.

## Por qué
D-01: el técnico debe saber que el cliente lo reportó, con texto neutro, sin acusar y sin penalización. Ningún tipo existente encajaba.

## A evitar
- Meter nombre, hora o datos del cliente o de la visita en el aviso: viajan solo identificadores.
- Poner el texto en la carga v1: la app lo elige por `evento` (spec 037, R-05).
- Inventar claves nuevas: se reutiliza `Services:Messaging:InternalKey`.
