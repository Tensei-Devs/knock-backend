# 2026-11-24 · KNOCK-34 · Enmienda de la spec 034: Front Door Standard permitido

## Qué cambió
La spec 034 lleva una enmienda fechada: R-11 y CA-10 permiten Front Door Standard (sin WAF ni Premium), tal como lo crea `deploy/modules/edge`. La tabla de criterios y el riesgo de recursos de pago se ajustan, y T-01 de `task.md` queda conciliada. Solo documentación.

## Por qué
Decisión de José: prevalece ADR-007. El límite de tasa se queda en la aplicación y no hay regla WAF.

## A evitar
- Subir a Premium o crear recursos WAF/CDN de pago.
- Dar por hecha la regla del análisis de Terraform: sigue sin implementarse (CA-10 sin prueba automática).
