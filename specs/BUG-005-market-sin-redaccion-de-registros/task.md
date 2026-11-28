# BUG-005 — tareas

> **Estado:** T1 a T4 hechas. La duplicación del redactor quedó resuelta por la spec 055 etapa 1 (`Knock.Hosting.Logging`).

## T1 — Confirmar y decidir [Docs]
- **Estado:** hecha; diagnóstico y decisión de Julián en la spec §8, reproducción sintética en T2.
- **Verificar:** diagnóstico y reproducción sintética; resolver con Julián la coordinación con KNOCK-251 y las dudas de §8.
- **Cubre:** TC-BUG-005-01 a 06.
- **No hacer:** tomar la propuesta de biblioteca como decisión aprobada.

## T2 — Pruebas de consola [Pruebas]
- **Estado:** hecha; TC-01 a 05 fallaron antes del arreglo y pasan después, junto con TC-07.
- **Crear o extender:** pruebas que capturen el camino real de consola de Market.
- **Verificar:** TC-BUG-005-01 a 05 y que fallen al omitir la redacción.
- **No hacer:** usar datos reales ni limitarse a probar el redactor aislado.

## T3 — Reutilizar y corregir [Back]
- **Estado:** hecha. Market escribe en consola con `RedactingJsonFormatter` y `TelemetryRedactor` de `Knock.Hosting.Logging`, los mismos de los demás servicios; ya no hay copia (retirada EXC-020 por la 055). Evidencia: `tests/UnitTest/Hosting/MarketConsoleRedactionTests.cs`.
- **Modificar:** configuración de consola de Market y, según decisión aprobada, ubicación compartida y consumidores del redactor.
- **Verificar:** TC-BUG-005-01 a 06, fronteras y regresión de servicios afectados.
- **No hacer:** copiar código ni introducir un clon nuevo o referencias entre servicios.

## T4 — Evidencia y memoria [Docs]
- **Estado:** hecha. Evidencia de ejecución: el CI del PR que cierra esta tarea (pruebas `MarketConsoleRedactionTests`, jscpd sin clon y `check_exceptions.py`); no se repitió la suite completa en local por la política de pruebas pesadas. Memoria en `docs/memoria/2026-11-23-KNOCK-251-bug-005-cierre.md`.
- **Verificar:** build, pruebas, formato, trazabilidad y jscpd; adjuntar resultados al PR.
- **Modificar:** documentación pertinente y `Memoria.md` con la decisión y resultados reales.
