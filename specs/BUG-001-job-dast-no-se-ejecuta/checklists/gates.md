# Checklist — BUG-001

> Estado: spec en Borrador. Las casillas las marca el revisor; no representan aprobación ni ejecución de pruebas.

- [ ] spec con 8 secciones + Aprobación, clave KNOCK-238 y estado Aprobada (revisor y fecha)
- [ ] Decisión de §8 sobre KNOCK-239 y KNOCK-240 tomada y escrita en la spec
- [ ] Cambio de `.github/workflows/ci-pr.yml` aplicado por una persona (T4)
- [ ] Cada caso TC-BUG-001-KK con su prueba o su salida de comando en el PR (01 a 08); `sdd_lint --trace BUG-001` en OK
- [ ] El job «Smoke + DAST (ZAP baseline)» se ejecutó en un PR real (no `skipped`)
- [ ] Sin secretos en el compose, en los scripts ni en los registros; Gitleaks en verde
- [ ] Imágenes por digest; sin llamadas a Azure; duración dentro del `timeout-minutes`
- [ ] `security-reviewer` LIBERA · `traceability-clerk` sin drift
- [ ] `docs/QA_DEVSECOPS.md`, `docs/modelo-de-amenazas.md` y `Memoria.md` actualizados · informe final en el PR
