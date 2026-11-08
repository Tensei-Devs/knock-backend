# KNOCK-78 · Resumen de la evidencia del escaneo ZAP

> **Alcance, leer primero.** El escaneo fue un **baseline pasivo** de menos de diez peticiones (`zap-baseline.py -m 1 -T 5`, ZAP 2.17.0, 2026-11-06). El rastreo solo alcanzó la raíz, `robots.txt` y `sitemap.xml`, porque todo lo demás responde 401. **No cubrió las rutas protegidas por autenticación** y **no constituye una validación completa de la seguridad del API**.

## Qué contiene esta carpeta

- `KNOCK-78-zap-baseline.html`, `.json` y `.md`: informe original de ZAP, sin modificar.
- Resultado: 66 reglas pasivas sin alerta; 0 altas, 0 medias, 1 baja (HSTS ausente en el gateway desplegado) y 1 informativa (contenido no almacenable, esperado).
- Análisis y hallazgos: `docs/modelo-de-amenazas.md`.

## Qué falta para una validación completa

- Un escaneo de API o activo con el OpenAPI del gateway y un token de prueba.
- Ejecutarlo solo en infraestructura temporal, creada y destruida con Terraform y sin costo, nunca contra el entorno desplegado.
- Con autorización del Tech Lead y fuera de la ventana de evaluación (25-nov al 5-dic de 2026).

## Defectos registrados a partir del análisis

KNOCK-238 (el job `dast` del CI no corre; prioridad alta), KNOCK-239 (HSTS ausente en el gateway), KNOCK-240 (encabezado `Server: Kestrel`) y KNOCK-241 (Mensajería sin JWT, encabezados ni límite).
