# 2026-11-23 · KNOCK-000 · Fechas corregidas y cuarta conciliación de task.md

## Qué cambió
- Las referencias a `2026-11-24` (fecha de hoy mal escrita) pasan a `2026-11-23` en `deploy/modules/edge/variables.tf`, las specs 034, 060, 062, 063 y 064, el comentario de `ModerationDomain.cs` y seis archivos de `docs/memoria`, que se renombran a `2026-11-23-*`. Solo cambia texto (comentarios, descripciones y notas); ninguna lógica. `Spec050VisitFieldsTests` conserva `2026-11-24` porque es el día ficticio de una visita en los datos de la prueba, no la fecha de un cambio.
- `task.md` de las specs 025 (T-03 a T-06), 037 (T-02 a T-04, con la columna de responsable restaurada), 043 (T-01), 046 (T-03), 055 (T-06), 056 (T-08) y 058 (T-03 y T-05) se concilia con el código; cada fila cita su evidencia. Lo que solo tiene pruebas unitarias o falta un cliente real queda como «parcial».

## Por qué
Una fecha de mañana en documentos y nombres de archivo confunde la trazabilidad, y los estados de `task.md` quedaron atrás del código ya fusionado.

## A evitar
- Marcar «hecha» una tarea cuyos casos de integración o cuyo cliente real (Blob de verificación, envío por FCM) no existen.
- Cambiar fechas de datos ficticios de pruebas junto con las fechas de los documentos.
