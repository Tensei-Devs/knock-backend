# Exportación de datos, rutas internas de Mercado para la cuenta y vencimiento de recuperaciones (spec 039b)

## Qué cambió
Identidad ahora ejecuta la tarea `PrepararExportacion`: junta sus datos y los de Mercado, arma un ZIP con un JSON por servicio (`identidad.json`, `mercado.json`), lo guarda detrás de `IExportStorage` (contenedor privado `exportaciones`), avisa por `IExportNotice` con un enlace de 24 horas y quita la marca de exportación en curso; si agota los reintentos también la quita. Las implementaciones de Azure Blob y del aviso aún no existen y fallan cerrado (`bloqueo: costo` hasta desplegar el contenedor). Terraform declara el contenedor `exportaciones` con ciclo de vida de 7 días y los permisos de Identidad, solo como cambio de archivos, sin aplicar.
Mercado tiene el módulo `Account` con rutas internas `/api/v1/interno/cuentas/{usuarioId}/pendientes`, `/cancelaciones`, `/exportacion` y `/eliminada`, con el permiso `cuenta` y el `usuarioId` del token igual al de la ruta. Identidad las llama con `MarketAccountClient`, que sustituye a `UnavailableMarketAccountClient` (sigue fallando cerrado con 503 si Mercado no responde). `/eliminada` es el consumidor de `CuentaEliminada`: cancela como tardías las visitas confirmadas, cierra lo abierto, borra lo personal de solicitudes, cotizaciones y perfil técnico, y deja el puerto `IOpinionAnonymizer` para la spec 051. La firma del token de servicio hacia Mercado quedó en `ServiceTokenIssuer`, que también usa el historial.
Identidad programa `VencerRecuperacion` al abrir un caso de recuperación y el manejador lo pasa a `vencida` a los 7 días si nadie lo decidió.

## Por qué
Spec 039, R-21, R-22, R-24 y R-25: la persona recibe sus datos en un archivo, la baja de cuenta cancela y anonimiza también en Mercado, y los casos de recuperación sin decidir no se quedan abiertos para siempre. Las rutas internas cierran la dependencia que dejaba `GET /me/pendientes` en 503.

## A evitar
Guardar el ZIP o su enlace en registros; incluir direcciones, códigos de visita o fotos en la exportación de Mercado; aceptar en las rutas internas un `usuarioId` que no sea el del token; firmar tokens de servicio fuera de `ServiceTokenIssuer`; dar por entregado un aviso o un archivo mientras las implementaciones de Azure no existan.
