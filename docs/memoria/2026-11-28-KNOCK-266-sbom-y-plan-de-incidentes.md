# KNOCK-266 · SBOM en el CI y plan de respuesta a incidentes

## Qué cambió

- `ci-pr` genera SBOM en CycloneDX: el job `sbom` (artefacto `sbom-dotnet`) lee los `packages.lock.json`, y el job `container` agrega al final el SBOM de cada imagen que construye (`sbom-<imagen>`). El job `sbom` entra en las dependencias de `gate`.
- `docs/plan-respuesta-incidentes.md`, enlazado desde `SECURITY.md`, y la brecha del SBOM actualizada en `docs/evaluacion/brechas-conocidas.md`.

## Por qué

- Se reutiliza la imagen de Trivy ya fijada por digest para el escaneo de contenedores: no entra ninguna herramienta, acción ni dependencia nueva, y el workflow no gana permisos.
- `ci-deploy.yml` no se tocó. La atestación de procedencia exige un permiso nuevo (`attestations: write`) y un paso en el job que publica, del que depende el despliegue, y ese flujo no se puede probar desde un PR.

## A evitar

- Presentar el SBOM de `ci-pr` como el de la imagen desplegada: `ci-deploy` vuelve a construirla.
- Dar por probados los procedimientos del plan: ninguno se ha ensayado. Quedan como huecos, entre otros, el reporte privado de vulnerabilidades desactivado, la reversión manual de `ci-deploy` sin ensayo y la falta de rotación para varias llaves.
- Fijar plazos de conservación en el plan: es una decisión pendiente del responsable del proyecto.
