# Spec 034 T-09: oasdiff con etiqueta contrato-incompatible

## Qué cambió
- `ci-pr.yml` (job `openapi-diff`): compara con oasdiff los tres contratos de servicio (Mercado, Identidad, Mensajería) contra la rama destino. Un cambio incompatible falla salvo con la etiqueta `contrato-incompatible`; con ella pasa, avisa y deja la lista en el resumen del PR (R-15, CA-12).
- Etiqueta `contrato-incompatible` creada en el repositorio.
- `task.md` de la 034: T-09 queda parcial.

## Por qué
Antes solo se comparaban dos contratos y no había forma explícita y visible de aceptar un cambio incompatible.

## A evitar
Falta el contrato público unificado (T-07, `--publico`): el diff del público se agrega cuando exista. No usar la etiqueta para saltarse cambios sin avisar al frontend.
