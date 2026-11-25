# 2026-11-24 · KNOCK-60 · Límites 429 y 413 de las rutas del panel (spec 053 T-11)

- **Qué cambió:** dos políticas de límite por sesión del panel (`sid`): `panel-lectura` (120 por minuto: VER-03 y REP-04) y `panel-decision` (30 por minuto: VER-04, VER-05 y REP-05), con 429 y `Retry-After`. `Knock.Hosting` gana `KnockRateLimitPolicies.PerKey` (clave que saca el servicio de la petición; sin clave cuenta por IP). Pruebas `CA_053_39` (429, sesión aparte y 413 con cuerpo mayor a 2 048 bytes).
- **Por qué:** las rutas del panel solo tenían los topes globales por IP (R-25, CA-39). El token del panel no pasa por el esquema de la app, así que `PerSubject` no lo ve; el `sid` se lee del token sin validar la firma, únicamente para repartir el contador (un token falso cuenta en su propia partición y la ruta lo rechaza con 401).
- **A evitar:** usar la clave sin validar para decidir acceso; dejar sin límite a una petición sin token legible (cuenta por IP).
