# 2026-11-23 · KNOCK-266 · Spec 034 T-03: ci-deploy con image_tag

- **Qué cambió:** `ci-deploy.yml` acepta `workflow_dispatch` con `image_tag`; el job `redeploy` usa el entorno `evaluacion`, valida 40 hexadecimales antes de iniciar sesión en Azure, exige que las cuatro imágenes existan en ACR y despliega esa etiqueta en los cuatro servicios.
- **Por qué:** R-07 (despliegue solo con acción humana) y R-08 (reversión a una imagen publicada) de la spec 034; CA-07 y CA-08.
- **Decisiones (con porqué):** (1) Los jobs del flujo normal ya se saltan en dispatch porque `detect` exige `workflow_run`. (2) La validación de existencia solo lee el registro; no cambia Azure hasta el último paso.
- **A evitar:** aceptar etiquetas como `latest` o ramas; desplegar si falta una de las cuatro imágenes.
- **Pendiente:** ejecución real documentada (CA-07/08) por una persona; crear el entorno `evaluacion` en GitHub con revisores si el plan lo permite.
