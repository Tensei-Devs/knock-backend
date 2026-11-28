# KNOCK-266 · Conteo previo de las solicitudes con la visita ya cerrada

## Qué cambió

- `db/README.md` trae la consulta de solo lectura que cuenta las solicitudes en `cita_confirmada` con la visita ya cerrada, y `task.md` de la spec 020 registra la tarea como pendiente.

## Por qué

- La enmienda del 2026-11-27 pide contar primero, después de desplegar la regla de estados, y solo escribir el script si hay afectadas. Todavía no hay conteo.

## A evitar

- Escribir o ejecutar el relleno antes del conteo.
- Dar la consulta por probada: no se ejecutó contra ninguna base.
