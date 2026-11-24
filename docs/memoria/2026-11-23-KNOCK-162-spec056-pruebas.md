# 2026-11-23 · KNOCK-162 · Pruebas de la spec 056 (T-05, T-06 y T-10)

## Qué cambió
Tres pruebas nuevas y ninguna línea de código de producción. `Spec037InternoAvisosSeguridadTests` suma `InternalNotices_CredentialWithoutThePermission_Returns403ServiceNotAllowed_CA_056_03` (credencial válida sin el permiso del tipo: 403 `service_not_allowed` y ningún aviso guardado; corre solo en el CI contra DocumentDB). `Spec056SecretosTests` (CA_056_08, sin contenedores) comprueba que ningún `appsettings*.json` trae valores de `Security:InternalTokens` ni de `SigningKey`, que el gateway no nombra las claves de servicio y que no tiene rutas `/interno`. `Spec056ReenvioDelJwtTests` (CA_056_12, sin contenedores) comprueba que `MarketVisitaCatalog` reenvía el JWT del usuario a `GET /api/v1/visitas/{id}`, sin otro encabezado de autenticación, y no reintenta tras un 401 o 403. `sdd_lint --trace 056` queda sin FALTA.

## Por qué
T-05, T-06 y T-10 de la spec 056 estaban pendientes o parciales y CA-03, CA-08 y CA-12 no tenían prueba con su ID.

## A evitar
- Dar T-06 por cerrada: falta probar el contenido de los registros de una llamada rechazada y aceptada, y la revisión de `security-reviewer`.
- Dar CA-12 por completo: el 401 de Mercado ante una credencial de servicio en la ruta pública solo está cubierto por la validación (CA_056_04).
