# Plan 067 — Sugerir el oficio con aprendizaje automático

> Parte de `spec.md` (estado Borrador; no habilita código). Redactado el 2026-11-28.

## Enfoque
1. Resolver D-01 y D-02 antes de escribir código.
2. Conjunto de evaluación escrito a mano **antes** de que sus autores vean los datos sintéticos.
3. Revisar la dependencia, generar los datos sintéticos con su ficha, entrenar y medir contra las líneas base.
4. Decidir con el reporte si se continúa.
5. Ruta con interruptor, pruebas, medición de recursos, contrato y memoria.

## Archivos que se crearían o cambiarían
`src/Knock.Market.Api/Modules/Assistant/`, un programa de entrenamiento y sus datos (carpeta por definir al implementar), `src/Knock.Gateway/appsettings.json`, `tests/`, `docs/` y una entrada en `docs/memoria/`. Archivos de bloqueo de paquetes y el Dockerfile de Mercado si el modelo lo requiere. `docs/openapi.v1.json` es un archivo protegido.

## Estimación sin medir
Backend: 8 a 13 horas. App (frontend 051): 3 a 5 horas.
