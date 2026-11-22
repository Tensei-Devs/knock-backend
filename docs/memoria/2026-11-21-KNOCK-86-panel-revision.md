# Panel de revisión: colas y decisiones de verificaciones y reportes (KNOCK-86)

## Qué cambió
- Módulo `Moderation` en Identidad con VER-03, VER-04, VER-05, REP-04 y REP-05 bajo `/api/v1/admin`, con permiso `verification:review` o `reports:resolve` de la sesión del panel (spec 033). El gateway enruta `/admin` con la política y el CORS del panel y cuenta el límite por `sub` del panel.
- Abrir una verificación escribe primero la auditoría `verificacion_abierta` y solo entonces pide enlaces de 5 minutos; decidir guarda estado, destruye la llave de las fotos y escribe la auditoría en una sola transacción de MongoDB.
- Las dependencias de otras specs entran por interfaces con valor por omisión que falla cerrado o no hace nada: fotos (025 T-04), recuperaciones (039), borrado de blobs (057) y efectos de la gravedad (054 y 059).
- `db/053-panel-de-revision.js` crea el índice de la cola; `AuditoriaPanel` gana la escritura dentro de una transacción ajena (`StoreTransaction`, para que los servicios no vean el controlador de MongoDB).

## Por qué
La spec 053 exige que la decisión y su registro sean atómicos y que nadie reciba enlaces si no queda constancia de quién los abrió.

## A evitar
- No registrar ni cachear enlaces de fotos, motivos ni textos de reportes.
- No reemplazar los valores por omisión de las interfaces sin la spec dueña: el de fotos responde 503 a propósito.
- No agregar permisos nuevos: la lista de la spec 033 es cerrada.

## PARA VALIDACIÓN DE JOSÉ
- Aprobar una verificación deja `purgadaEn` y la llave destruida también al aprobar (spec 057, D-03).
- Quien decide o abre no puede ser la persona verificada ni parte del reporte (403 `self_review_not_allowed`).