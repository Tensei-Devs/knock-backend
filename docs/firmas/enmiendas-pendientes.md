# Enmiendas pendientes de firma

Generado por `scripts/sdd_sign_amendments.py` v1.0.0 el 2026-11-25.
Origen: ref `origin/docs/KNOCK-60-enmiendas-044-039-054`. Base de comparación: `origin/sprint-05`.

**Este archivo no firma nada.** Lo genera el mecanismo; la firma la hace una persona con `sign`, en su terminal, escribiendo su nombre real y la frase de confirmación. Un agente no firma.

## Aprobación conjunta propuesta

Firmar en este orden, una por una (cada `sign` vuelve a pedir confirmación interactiva):

1. `specs/039-cuenta-y-datos-personales/spec.md` — Enmienda 2026-11-24 (+9 −0)

   ```
   python -X utf8 scripts/sdd_sign_amendments.py sign --spec 039-cuenta-y-datos-personales --label "Enmienda 2026-11-24" --signer "José Julián Hernández Hernández"
   ```

2. `specs/044-avisos-dentro-de-la-app/spec.md` — Enmienda 2026-11-24 (+9 −0)

   ```
   python -X utf8 scripts/sdd_sign_amendments.py sign --spec 044-avisos-dentro-de-la-app --label "Enmienda 2026-11-24" --signer "José Julián Hernández Hernández"
   ```

3. `specs/054-bandeja-de-salida-y-tareas-programadas/spec.md` — Enmienda 2026-11-24 (+5 −0)

   ```
   python -X utf8 scripts/sdd_sign_amendments.py sign --spec 054-bandeja-de-salida-y-tareas-programadas --label "Enmienda 2026-11-24" --signer "José Julián Hernández Hernández"
   ```

Después de firmar todas, revisar y subir el cambio como un commit normal (sin reescribir historial, sin `--amend` de commits ya empujados):

```
git diff --stat
git add specs docs/firmas
git commit -m "docs(specs): sign the pending amendments (KNOCK-<N de la spec>)"
python -X utf8 scripts/sdd_sign_amendments.py verify
```

## No firmables todavía

| Spec | Enmienda | Clasificación | Motivo |
|---|---|---|---|

## Decisiones abiertas (antes de escribir la enmienda)

| ID | Spec | Tema | Estado | Decisión o pregunta | Evidencia |
|---|---|---|---|---|---|
| D-020-01 | 020-solicitudes-y-cotizaciones | Transiciones de estado sin especificar (T-04) | pendiente de decisión de José | ¿Qué transiciones de solicitud y de cotización se aceptan y con qué resultado? La tabla tiene 18 filas de solicitud y 11 de cotización; ninguna regla pasa de `cita_confirmada` a `terminada`, así que `calificada` no se alcanza. | `_herramientas/plan/020_T04_TRANSICIONES_FALTANTES.md`; plan §8 (decisión 3) |
| D-033-01 | 033-panel-interno-con-permisos-y-doble-factor | Detalle campo a campo de la auditoría (R-23) | pendiente de decisión de José | `panel_auditoria` no admite valores: con la auditoría por outbox solo se guarda acción, clave del oficio y `sub`. ¿Se enmienda R-23 para aceptar ese detalle, o se deja así? | #394; plan §8 (C2) |
| D-043-01 | 043-bloqueos-y-reportes | Filtro del técnico bloqueado en mapa y autocompletar | pendiente de decisión de José (opcional) | `GET /tecnicos`, Explorar y la lista de cotizaciones ya excluyen al bloqueado; `/mapa/tecnicos`, autocompletar y «trabajos reales» no filtran (son anónimos o cacheados públicamente). ¿Se enmienda 046 para filtrarlos también? | #389; plan §8 (B4) |
| D-045-01 | 045-catalogos-y-lista-de-espera | Constancia de la auditoría por outbox (R-20) | pendiente de redactar la enmienda | El cambio de comportamiento ya está fusionado: el oficio se escribe y el evento se reintenta; si la auditoría falla queda `fallido` en la bandeja. Falta la enmienda que deje constancia de R-20 y de que no se guarda el detalle campo a campo. | #394; plan §8 (C2) |
| D-046-01 | 046-tecnico-perfil-horarios-y-exploracion | Índices y búsqueda de texto (T-01) | pendiente de decisión de José | La spec pide índices 2dsphere sobre `ubicacion`, texto en español y `reputacion.promedio`; el código usa `ubicacionBase` y `palabrasBusqueda`. La búsqueda de texto **no** se conserva completa: el autocompletar no devuelve «servicios» y los negocios se buscan con regex sensible a acentos. ¿Se enmienda a lo implementado, se agrega un script `db/` o se corrige el código? | `_herramientas/plan/046_T01_VERIFICACION_INDICES.md`; plan §8 (decisión 2) |
| D-052-01 | 052-asistente-de-ia | Contradicción de CA-23 | pendiente de decisión de José | CA-23 pide «201 con ProblemDetails» cuando el recurso de IA no está disponible, y R-12/R-15 piden 503 `ai_unavailable`. ¿Se enmienda CA-23 a 503 o se elimina el caso? Es el único CA de 052 sin prueba. | #393; plan §8 (C3) |

## Diferencias contra la versión anterior

### specs/039-cuenta-y-datos-personales/spec.md — Enmienda 2026-11-24

`+9 −0`

```diff
diff --git a/specs/039-cuenta-y-datos-personales/spec.md b/specs/039-cuenta-y-datos-personales/spec.md
index 660582e3..56a19829 100644
--- a/specs/039-cuenta-y-datos-personales/spec.md
+++ b/specs/039-cuenta-y-datos-personales/spec.md
@@ -312,7 +312,16 @@ Las pruebas citan el caso como `CA_039_NN` en el nombre (para `python scripts/sd
 - Tarea: `task.md` T-15, desbloqueada por la firma del 2026-11-24 (pendiente de código). Requiere también el tipo `exportacion_lista` en la spec 037 (T-10 y el catálogo cerrado de `AvisoRules`).
 
+## Nota de enmienda (2026-11-24, decisión de José, pendiente de firma)
+- **Estado de esta enmienda:** **sin firmar**. Propuesta para validación de José Julián Hernández Hernández; no habilita nada por sí sola. El código ya está en un PR aparte (Nivel C) que se fusiona solo después de esta firma. La firma es la línea «Enmienda 2026-11-24» de la sección Aprobación.
+- **R-22: qué lleva `mensajeria.json`.** El ZIP trae un JSON por servicio: `identidad.json`, `mercado.json` y `mensajeria.json`. `mensajeria.json` contiene **únicamente los mensajes que la persona escribió**, agrupados por conversación y con su rol en ella (`cliente` o `tecnico`): identificador del mensaje, texto (ya con el contacto ocultado, como se guardó), si se ocultó un contacto y fecha. **No** lleva los mensajes de la otra parte, ni su identificador, ni el `autorId`. Es coherente con «contiene solo datos propios».
+- **Ruta interna de Mensajería.** Identidad lee esos datos con `GET /api/v1/interno/cuentas/{usuarioId}/exportacion` de Mensajería (la spec 054 R-15 la nombra `/api/v1/interno/exportacion`; se fija este nombre porque sigue el de Mercado, `/interno/cuentas/{usuarioId}/exportacion`). Token de servicio de la spec 037: emisor `knock-identity`, clave `avisos-key-identity`, `aud` `knock-notifications`, `exp` de 1 minuto, `permisos` con `exportacion` y el claim `usuarioId` igual al de la ruta; un token de otra persona, sin el permiso o de otro emisor responde 403 `service_not_allowed`. Fuera del gateway, sin caché. Si Mensajería no responde, la tarea falla y se reintenta: nunca se guarda un ZIP sin `mensajeria.json`.
+- **Límites.** Hasta 1000 conversaciones por rol y 5000 mensajes propios por conversación; los mensajes ya vencen a los 180 días (spec 030, R-21). No hay índice nuevo: se usan los de conversaciones por `clienteId`/`tecnicoUsuarioId` y el de mensajes por conversación.
+- **Sin cambio en Azure.** El `IExportStorage` real (contenedor `exportaciones`) sigue detrás del interruptor apagado y de Terraform; mientras tanto `GET /me/exportaciones/ultima` en estado `lista` responde 503.
+- Tarea: `task.md` T-05 y T-11 (parte de Mensajería); `054` T-09 (ruta interna de exportación).
+
 ## Aprobación
 - [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-14
 - [x] Enmienda 2026-11-23 — nombre: José Julián Hernández Hernández fecha: 2026-11-24
+- [ ] Enmienda 2026-11-24 — nombre: ________ fecha: ________
 - [x] Sin conflicto con la constitución
 - **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
```

### specs/044-avisos-dentro-de-la-app/spec.md — Enmienda 2026-11-24

`+9 −0`

```diff
diff --git a/specs/044-avisos-dentro-de-la-app/spec.md b/specs/044-avisos-dentro-de-la-app/spec.md
index 5cb15754..74a8dd78 100644
--- a/specs/044-avisos-dentro-de-la-app/spec.md
+++ b/specs/044-avisos-dentro-de-la-app/spec.md
@@ -240,7 +240,16 @@ Las pruebas citan el caso como `CA_044_NN` en el nombre (para `python scripts/sd
 - Tarea: `task.md` T-11, desbloqueada por la firma del 2026-11-24 (pendiente de código).
 
+## Nota de enmienda (2026-11-24, decisión de José, pendiente de firma)
+- **Estado de esta enmienda:** **sin firmar**. Propuesta para validación de José Julián Hernández Hernández; no habilita nada por sí sola. El código que pide ya está en un PR aparte (Nivel C) que se fusiona solo después de esta firma. La firma es la línea «Enmienda 2026-11-24» de la sección Aprobación.
+- **Tipo `sistema`, no apagable.** R-13 y D-06 dicen que solo los avisos de `seguridad` no se apagan. La lista cerrada de tipos (`NoticeRules.Types`) gana `sistema`, que agrupa los avisos de texto fijo `reporte_decidido`, `opinion_oculta` y `exportacion_lista` (catálogo de la spec 037). `GET /api/v1/me/preferencias-avisos` devuelve ese tipo con `activo: true` y `apagable: false`, igual que `seguridad`; un `PUT` que intente apagarlo responde 422 y no se guarda. Con esto CA-19 se lee «todos los tipos activos salvo promociones, y los de seguridad y sistema marcados como no apagables».
+- **Avisos sin rol.** Los avisos que no pertenecen a un rol (`reporte_decidido` y `exportacion_lista`) se guardan con `comoRol` nulo (antes el despachador les ponía `cliente`, por lo que un técnico no los veía). El filtro `comoRol` de `GET /api/v1/avisos` devuelve los del rol pedido **más** los comunes (`comoRol` nulo), tanto para `cliente` como para `tecnico`; sin filtro devuelve todos. En la respuesta `comoRol` puede ser nulo. CA-16 se lee «ve solo los de ese rol y los comunes». El contador de no leídos no filtra por rol, así que no cambia.
+- **Sin cambio de datos ni de índices.** Los índices de `db/044-avisos-dentro-de-la-app.js` no incluyen `comoRol`; el filtro es sobre el resultado de la consulta por persona, y no se necesita migración. Los avisos ya guardados con rol `cliente` por esta causa (solo `reporte_decidido` y `exportacion_lista` creados desde que existe el despachador) conservan ese rol hasta que vencen a los 30 días.
+- **Sobre `GET /api/v1/avisos?tipo=sistema`.** La ruta no tiene hoy un filtro `tipo` (R-02: cursor, limite, leido, comoRol); `tipo` es un campo de la respuesta y la clave de las preferencias. El PR de código **no agrega** ese filtro; si José lo quiere, es una enmienda aparte de R-02 y de la tabla de entrada.
+- Tarea: `task.md` T-11 (código en el PR de Nivel C de la rama `feature/KNOCK-60-avisos-sistema-export`).
+
 ## Aprobación
 - [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-14
 - [x] Enmienda 2026-11-23 — nombre: José Julián Hernández Hernández fecha: 2026-11-24
+- [ ] Enmienda 2026-11-24 — nombre: ________ fecha: ________
 - [x] Sin conflicto con la constitución
 - **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
```

### specs/054-bandeja-de-salida-y-tareas-programadas/spec.md — Enmienda 2026-11-24

`+5 −0`

```diff
diff --git a/specs/054-bandeja-de-salida-y-tareas-programadas/spec.md b/specs/054-bandeja-de-salida-y-tareas-programadas/spec.md
index 01b12776..39059c35 100644
--- a/specs/054-bandeja-de-salida-y-tareas-programadas/spec.md
+++ b/specs/054-bandeja-de-salida-y-tareas-programadas/spec.md
@@ -209,6 +209,11 @@ Las pruebas citan el caso como `CA_054_NN` en el nombre (para `python scripts/sd
 - **Riesgo aceptado:** si Mensajería necesita tareas propias, esta spec se amplía.
 
+## Nota de enmienda (2026-11-24, pendiente de firma)
+- **Estado de esta enmienda:** **sin firmar**; propuesta para validación de José Julián Hernández Hernández.
+- **R-15, ruta de exportación de Mensajería.** La ruta nombrada `/api/v1/interno/exportacion` se implementa como `GET /api/v1/interno/cuentas/{usuarioId}/exportacion` en Mensajería (igual que la de Mercado), con el permiso `exportacion` y el `usuarioId` en el token (detalle en la nota de la spec 039 de la misma fecha). Solo la llama Identidad.
+
 ## Aprobación
 - [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-14
+- [ ] Enmienda 2026-11-24 — nombre: ________ fecha: ________
 - [x] Sin conflicto con la constitución
 - **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
```

