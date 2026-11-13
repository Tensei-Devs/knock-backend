# BUG-005 — tareas

> **Estado:** pendientes. No se ejecutan hasta que la spec esté Aprobada y una persona lo indique.

## T1 — Confirmar y decidir [Docs]
- **Verificar:** diagnóstico y reproducción sintética; resolver con Julián la coordinación con KNOCK-251 y las dudas de §8.
- **Cubre:** TC-BUG-005-01 a 06.
- **No hacer:** tomar la propuesta de biblioteca como decisión aprobada.

## T2 — Pruebas de consola [Pruebas]
- **Crear o extender:** pruebas que capturen el camino real de consola de Market.
- **Verificar:** TC-BUG-005-01 a 05 y que fallen al omitir la redacción.
- **No hacer:** usar datos reales ni limitarse a probar el redactor aislado.

## T3 — Reutilizar y corregir [Back]
- **Modificar:** configuración de consola de Market y, según decisión aprobada, ubicación compartida y consumidores del redactor.
- **Verificar:** TC-BUG-005-01 a 06, fronteras y regresión de servicios afectados.
- **No hacer:** copiar código ni introducir un clon nuevo o referencias entre servicios.

## T4 — Evidencia y memoria [Docs]
- **Verificar:** build, pruebas, formato, trazabilidad y jscpd; adjuntar resultados al PR.
- **Modificar:** documentación pertinente y `Memoria.md` con la decisión y resultados reales.
