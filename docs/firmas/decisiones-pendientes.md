# Decisiones pendientes antes de escribir o firmar una enmienda

Este archivo lo lee `scripts/sdd_sign_amendments.py`: cada fila es una decisión de negocio o de
especificación que **no** se puede firmar todavía porque no hay una enmienda cerrada, o porque la
enmienda escrita contradice el código o la spec. La columna `Estado` debe decir `decidida` cuando
José la resuelva (entonces la fila deja de aparecer en el inventario) y la enmienda se escribe en
su `spec.md` con su nota y su línea de firma.

No se inventa la decisión: la pregunta y la evidencia quedan aquí tal como están en el plan y en
los informes de los agentes.

| ID | Spec | Tema | Estado | Decisión o pregunta | Evidencia |
|---|---|---|---|---|---|
| D-020-01 | 020-solicitudes-y-cotizaciones | Transiciones de estado sin especificar (T-04) | decidida (enmienda firmada, 2026-11-26) | ¿Qué transiciones de solicitud y de cotización se aceptan y con qué resultado? La tabla tiene 18 filas de solicitud y 11 de cotización; ninguna regla pasa de `cita_confirmada` a `terminada`, así que `calificada` no se alcanza. | `_herramientas/plan/020_T04_TRANSICIONES_FALTANTES.md`; plan §8 (decisión 3) |
| D-033-01 | 033-panel-interno-con-permisos-y-doble-factor | Detalle campo a campo de la auditoría (R-23) | pendiente de decisión de José | `panel_auditoria` no admite valores: con la auditoría por outbox solo se guarda acción, clave del oficio y `sub`. ¿Se enmienda R-23 para aceptar ese detalle, o se deja así? | #394; plan §8 (C2) |
| D-043-01 | 043-bloqueos-y-reportes | Filtro del técnico bloqueado en mapa y autocompletar | pendiente de decisión de José (opcional) | `GET /tecnicos`, Explorar y la lista de cotizaciones ya excluyen al bloqueado; `/mapa/tecnicos`, autocompletar y «trabajos reales» no filtran (son anónimos o cacheados públicamente). ¿Se enmienda 046 para filtrarlos también? | #389; plan §8 (B4) |
| D-045-01 | 045-catalogos-y-lista-de-espera | Constancia de la auditoría por outbox (R-20) | decidida (enmienda firmada, 2026-11-26) | El cambio de comportamiento ya está fusionado: el oficio se escribe y el evento se reintenta; si la auditoría falla queda `fallido` en la bandeja. Falta la enmienda que deje constancia de R-20 y de que no se guarda el detalle campo a campo. | #394; plan §8 (C2) |
| D-046-01 | 046-tecnico-perfil-horarios-y-exploracion | Índices y búsqueda de texto (T-01) | decidida (enmienda firmada, 2026-11-26) | La spec pide índices 2dsphere sobre `ubicacion`, texto en español y `reputacion.promedio`; el código usa `ubicacionBase` y `palabrasBusqueda`. La búsqueda de texto **no** se conserva completa: el autocompletar no devuelve «servicios» y los negocios se buscan con regex sensible a acentos. ¿Se enmienda a lo implementado, se agrega un script `db/` o se corrige el código? | `_herramientas/plan/046_T01_VERIFICACION_INDICES.md`; plan §8 (decisión 2) |
| D-052-01 | 052-asistente-de-ia | Contradicción de CA-23 | decidida (enmienda firmada, 2026-11-26) | CA-23 pide «201 con ProblemDetails» cuando el recurso de IA no está disponible, y R-12/R-15 piden 503 `ai_unavailable`. ¿Se enmienda CA-23 a 503 o se elimina el caso? Es el único CA de 052 sin prueba. | #393; plan §8 (C3) |
