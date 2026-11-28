# Plan 065 — App Check en observación

> Parte de `spec.md` (estado Borrador; no habilita código). Redactado el 2026-11-28.

## Enfoque
1. Opciones y validación de arranque (`off` por omisión), con pruebas primero.
2. Verificador y llaves en segundo plano, con llaves de prueba.
3. Middleware de observación, métrica, registro con tope y retiro del encabezado.
4. Pruebas de integración y de seguridad, documentación y memoria.

## Archivos que se crearían o cambiarían
`src/Knock.Gateway/`, `tests/UnitTest/Gateway/`, `tests/IntegrationTest/Gateway/`, `tests/SecurityTest/Gateway/`, `docs/` y una entrada en `docs/memoria/`. Cualquier cambio en `deploy/**` va aparte y con autorización.

## Estimación sin medir
4 a 6 horas de código y pruebas, sin contar el CI, la revisión ni el despliegue. La consola de Firebase se estima en frontend 050.
