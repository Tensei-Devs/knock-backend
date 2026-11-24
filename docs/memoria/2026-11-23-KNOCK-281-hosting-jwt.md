# 2026-11-23 · KNOCK-281 · JwtSettings y parámetros de validación del JWT pasan a Knock.Hosting (spec 055, T-17 parcial)

## Qué cambió
`JwtSettings` y `JwtSettingsFactory` viven en `Knock.Hosting.Security`; se borraron las copias de Identidad, Mercado y Mensajería (`JwtSettings.cs` y `JwtSettingsFactory.cs`) y el `GatewayJwtSettings` del gateway. `JwtSettings.ToValidationParameters()` es el único lugar donde se arman los parámetros del JWT propio (HS256, firmante, destinatario y vencimiento obligatorios, sin tolerancia de reloj) y lo usan los cuatro proyectos. `JwtSettingsFactory.Create(configuration, environment)` conserva la clave aleatoria solo en Development; `CreateRequiringKey(configuration)` es la variante del gateway, que exige la clave siempre, con los mismos mensajes que antes. Se ajustan EXC-016 y EXC-021 (ya no nombran estas piezas).

## Por qué
Etapa 3 de la spec 055 (R-08): una sola implementación de las piezas del catálogo. Las demás piezas de T-17 (encabezados de seguridad, HSTS, `ProblemResponses`, CORS, límite de cuerpo) quedan para sus propios PR porque entre copias hay diferencias de comportamiento que hay que parametrizar.

## A evitar
- Armar `TokenValidationParameters` a mano para el JWT propio en un servicio.
- Unificar la clave aleatoria de Development con la del gateway: el gateway no la genera.
