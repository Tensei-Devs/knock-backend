# 2026-11-23 · KNOCK-000 · Checkov CKV_GHA_7 en el input `image_tag` de ci-deploy

- **Qué cambió:** `.github/workflows/ci-deploy.yml` lleva `#checkov:skip=CKV_GHA_7: EXC-031` bajo `workflow_dispatch`, y `security/exceptions.yaml` registra EXC-031 (alcance solo ese input, vence 2027-02-18).
- **Por qué:** la regla exige que los inputs de `workflow_dispatch` estén vacíos y no hay forma de cumplirla conservando la reversión manual de la spec 034 (se necesita elegir el SHA). El input solo lo usa el job `redeploy` (valida 40 hex, exige las imágenes en ACR, solo hace `az containerapp update`); ningún build lo lee.
- **A evitar:** pasar `image_tag` a jobs de build o publicación; ampliar la supresión a otros inputs.
