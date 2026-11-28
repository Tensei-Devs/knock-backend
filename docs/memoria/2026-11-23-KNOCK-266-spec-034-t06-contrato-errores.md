# 2026-11-23 · KNOCK-266 · Spec 034 T-06: contrato de errores común

- **Qué cambió:** `Knock.Hosting.OpenApi.ContractConventions` agrega un transformador de documento OpenAPI que los tres servicios registran: `ProblemDetails` declara `code`, `correlationId` y `traceId`; cada respuesta declara `X-Correlation-Id`; 429 y 503 declaran `Retry-After`. Los tres contratos se regeneraron con `scripts/export_openapi.py` (cambio aditivo). `Knock.Hosting` referencia `Microsoft.AspNetCore.OpenApi` 11.0.0 (el mismo paquete que ya usan los servicios). Prueba `Spec034ContratoTests.Errores_Y_Encabezados_Declarados_CA_034_15`.
- **Por qué:** el frontend generaba clientes sin saber que los errores traen `code` y `correlationId` ni que 429 y 503 piden esperar (spec 034 R-14, CA-15).
- **A evitar:** declarar a mano estos campos en un controlador; el transformador es la única fuente. El contrato solo describe lo que el middleware de correlación y el manejador de errores ya envían.
