# Mecanismo único para inventariar y firmar enmiendas de spec

## Qué cambió
- Nuevo `scripts/sdd_sign_amendments.py` con cuatro subcomandos: `inventory` (enmiendas pendientes separadas en firmables, con decisiones abiertas, incompletas y no firmables), `prepare` (paquete de aprobación conjunta en `docs/firmas/enmiendas-pendientes.md`), `sign` (firma una enmienda con confirmación interactiva) y `verify` (comprueba las firmas registradas).
- Nuevo libro append-only `docs/firmas/enmiendas.jsonl`: una línea por firma con firmante, fecha, commit, SHA-256 de la nota de enmienda y de la spec antes y después. `docs/firmas/decisiones-pendientes.md` reúne las decisiones que bloquean una enmienda (020 T-04, 033 R-23, 043 mapa/autocompletar, 045 R-20, 046 T-01, 052 CA-23). `docs/firmas/README.md` explica el flujo.
- Pruebas en `scripts/test_sdd_sign_amendments.py` (19 casos, sin git ni red) y paso nuevo en `.github/workflows/_governance.yml` para correrlas en el CI.
- Inventario real con el mecanismo: las tres enmiendas del 2026-11-24 (039, 044 y 054, las de #391) salen **firmables**; el resto de lo pendiente son las seis decisiones abiertas. El paquete quedó en `docs/firmas/enmiendas-pendientes.md`.

## Por qué
- José pidió un único mecanismo que inventaríe, compare con la versión anterior y registre firmas verificables sin reescribir historial. Antes cada enmienda se firmaba a mano y no quedaba constancia del texto firmado.
- El agente no puede firmar: `sign` exige terminal interactiva (falla en CI y por tubería), que el firmante ya conste en la sección Aprobación de la spec, la spec limpia respecto a `HEAD`, la frase exacta de confirmación y el nombre completo. Se niega si hay marcadores de decisión abierta, si falta la nota de enmienda, si la spec está en Borrador o si la línea ya está marcada.

## A evitar
- Firmar por lote sin leer: `prepare` agrupa las firmables, pero cada `sign` vuelve a pedir confirmación; no hay opción de firmar todas de una vez.
- Editar `docs/firmas/enmiendas.jsonl` a mano o reescribir commits ya empujados: una firma equivocada se corrige con un commit posterior.
- Bajar la clasificación a "firmable" quitando marcadores del texto: si la enmienda tiene una pregunta abierta, se resuelve primero en `decisiones-pendientes.md`.
