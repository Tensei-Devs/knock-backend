# Firmas de enmiendas de spec

El mecanismo es `scripts/sdd_sign_amendments.py`. Sirve para inventariar las enmiendas pendientes,
separar las firmables de las que necesitan una decisión, compararlas con la versión anterior y
registrar una firma verificable. **No firma por nadie**: el agente prepara; José firma.

## Qué hay aquí

- `enmiendas-pendientes.md`: paquete de aprobación conjunta generado con `prepare` (se regenera; no
  se edita a mano).
- `decisiones-pendientes.md`: decisiones que bloquean una enmienda; lo lee el inventario.
- `enmiendas.jsonl`: libro append-only de firmas. Una línea JSON por firma, con el SHA-256 de la
  nota de enmienda, el de la spec antes y después y el commit sobre el que se firmó. **No se
  reescribe ni se borra**: una firma equivocada se corrige con un commit posterior, no reescribiendo
  el historial.

## Uso

```bash
python -X utf8 scripts/sdd_sign_amendments.py inventory            # qué hay pendiente y por qué
python -X utf8 scripts/sdd_sign_amendments.py inventory --ref origin/docs/KNOCK-60-enmiendas-044-039-054
python -X utf8 scripts/sdd_sign_amendments.py prepare              # paquete de aprobación conjunta
python -X utf8 scripts/sdd_sign_amendments.py sign --spec 039-cuenta-y-datos-personales \
    --label "Enmienda 2026-11-24" --signer "José Julián Hernández Hernández"
python -X utf8 scripts/sdd_sign_amendments.py verify
```

## Reglas del mecanismo

- `sign` exige una terminal interactiva (falla en CI y por tubería), que el firmante sea una persona
  ya nombrada en la sección Aprobación de la spec, que la spec esté limpia respecto a `HEAD` y que se
  escriba la frase exacta de confirmación y el nombre completo.
- `sign` se niega si la enmienda tiene marcadores de decisión abierta, si no tiene nota de enmienda,
  si la spec está en Borrador o si la línea ya está marcada.
- La firma es un commit normal: `- [ ]` pasa a `- [x]` y se añade la línea al libro. Sin `--amend`
  sobre commits ya empujados, sin rebase de historial.
- `verify` compara el texto actual y el de `HEAD` con el registrado: distingue `committed`,
  `pendiente-de-commit` y `deriva`, y avisa de firmas anteriores al mecanismo que no tienen registro.
