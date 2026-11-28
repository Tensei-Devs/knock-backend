# 2026-11-23 · KNOCK-186 · Spec 035: cierre de T-04 y T-09

- **Qué cambió:** pruebas de seguridad `Messaging_ProblemDetails_400/422_Sin_Consulta_CA_035_07` (host real de Mensajería, `instance` con la plantilla de la ruta y traza sin consulta); `docs/rate-limit-matrix.md` con la matriz por política verificada contra el código; script de humo `tests/smoke/spec035-dos-clientes.sh` (CA-15).
- **Por qué:** quedaban parciales T-04 (400 y 422 en Mensajería) y T-09 (matriz y humo).
- **A evitar:** dar por cumplido CA-15 sin correr el humo contra evaluación con dos IP públicas distintas; documentar un límite sin comprobarlo en el código de las políticas.
