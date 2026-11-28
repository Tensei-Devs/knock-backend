# Avisos de sistema, avisos sin rol y exportación de Mensajería

## Qué cambió
- `sistema` entra a `NoticeRules.Types` como tipo no apagable (igual que `seguridad`); el validador de preferencias rechaza apagarlo con 422.
- Los avisos sin rol (`reporte_decidido`, `exportacion_lista`) se guardan con `comoRol` nulo; el filtro `comoRol` devuelve los del rol pedido y los comunes. `comoRol` de la respuesta puede ser nulo. Sin índice ni migración nuevos.
- Mensajería: `GET /api/v1/interno/cuentas/{usuarioId}/exportacion` (solo Identidad, permiso `exportacion`, `usuarioId` del token igual al de la ruta) devuelve solo los mensajes propios. Identidad: `MessagingExportClient` y `mensajeria.json` en el ZIP; si Mensajería falla, la tarea se reintenta y no se guarda ningún ZIP.
- `ServiceTokenIssuer.IssueForMessaging` (misma clave `avisos-key-identity`).

## Por qué
- Decisiones de José del 2026-11-24 (enmiendas sin firmar de las specs 044, 039 y 054 en un PR aparte).

## A evitar
- No fusionar antes de la firma de las enmiendas. No encender el `IExportStorage` de Azure: sigue apagado y por Terraform.
- No exportar mensajes de la otra parte ni su identificador.
