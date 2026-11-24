# 2026-11-23 · KNOCK-000 · El módulo de borde solo acepta Front Door Standard

## Qué cambió
La validación de `sku_name` en `deploy/modules/edge/variables.tf` ya no acepta `Premium_AzureFrontDoor`: solo `Standard_AzureFrontDoor` (valor por omisión sin cambio). `edge.tftest.hcl` suma `sku_premium_se_rechaza` (`expect_failures`) y `sku_standard_explicito_es_valido`. La tarea T-01 de la spec 034 queda marcada con esta evidencia (CA-10).

## Por qué
La enmienda del 2026-11-23 (ADR-007, spec 034 R-11) prohíbe el SKU Premium (unos 330 USD al mes). Antes la regla solo existía como texto.

## A evitar
- Reabrir la lista de SKU permitidos sin una nueva decisión de José.
- Cubrir la regla solo con un comentario: la prueba de `terraform test` es la que la fija.
