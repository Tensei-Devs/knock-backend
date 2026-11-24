# 2026-11-23 · KNOCK-86 · Pruebas de integración de la cola, la apertura y la decisión de verificaciones (spec 053)

## Qué cambió
Se agrega `Spec053VerificacionesPanelTests` (integración, MongoDB y Redis reales, rutas reales del panel) para VER-03 a VER-05: la cola con sus datos y su cursor (CA-05, CA-30), la apertura con los enlaces de 5 minutos y su auditoría sin los enlaces (CA-06, CA-31), 404 y 410 (CA-10, CA-11), la aprobación que destruye la llave y las fotos y audita en la misma escritura (CA-12, CA-32), el rechazo con motivo (CA-15), 404 y 409 (CA-17, CA-18), la decisión repetida y diez simultáneas (CA-40), el cuerpo que no cambia a quien decide (CA-37) y la persona verificada que no revisa la suya (D-05). `task.md` de la 053 actualiza T-02 a T-05. Sin código de producción ni contrato.

## Por qué
`sdd_lint --trace 053` listaba CA-06, 12 y 40 sin prueba y la 053 no tenía ninguna prueba de integración de las verificaciones con datos reales. CA-39 (429 del panel) sigue sin prueba: espera la firma de la enmienda de #358.

## A evitar
- Buscar el texto de un identificador al azar dentro del cuerpo crudo: se comparan campos.
- Dar por probada la apertura con el almacén real: el host de pruebas solo emite enlaces (sin Blob).
- Cerrar T-04 sin una implementación real de `IAccessRecoveryReviews` (spec 039).
