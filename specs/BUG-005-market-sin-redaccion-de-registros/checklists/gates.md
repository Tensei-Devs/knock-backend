# Checklist — BUG-005

> Estado: spec en Borrador. Las casillas las marca el revisor; no representan aprobación ni ejecución de pruebas.

- [ ] Spec con 8 secciones + Aprobación, clave KNOCK-255 y estado Aprobada por Julián con fecha
- [ ] Diagnóstico contrastado y reproducción sintética documentada
- [ ] Decisiones de §8 sobre KNOCK-251, ubicación compartida y casos de redacción resueltas
- [ ] Cada caso TC-BUG-005-KK con prueba o salida de comando en el PR (01 a 06); `sdd_lint --trace BUG-005` en OK
- [ ] Pruebas fallan al omitir la redacción y pasan con la corrección; consola conserva JSON válido
- [ ] jscpd no reporta un clon nuevo; fronteras y regresión de servicios afectados verificadas
- [ ] Build, pruebas y formato en verde; datos de prueba sintéticos
- [ ] Revisión de seguridad y trazabilidad sin hallazgos bloqueantes
- [ ] `Memoria.md` actualizada e informe final en el PR
