# 2026-11-23 · KNOCK-266 · Se eliminan tres clones que rompían el gobierno (jscpd)

- **Qué cambió:** los dos controladores internos de entrega de eventos de Mercado en Identidad comparten ahora `InternalEventDeliveryController<TBody>` (límite de cuerpo, respuestas documentadas, `Cache-Control: private, no-store`, 202); cada uno solo fija ruta, permiso y consumidor. En pruebas, `PanelTestHost` reúne el arranque y el token del panel que repetían `Spec045AdminHttpTests` y `Spec061OcultarOpinionTests`, y `Spec037InternoAvisosSeguridadTests` usa un único método para crear su host.
- **Por qué:** `jscpd` (`.jscpd.json`, `exitCode 1`) fallaba en `sprint-05` por esos clones; no se agregan ignores ni excepciones.
- **A evitar:** suprimir clones con `ignore` en `.jscpd.json`; cambiar rutas, permisos o respuestas al compartir código (OpenAPI regenerado sin diferencias).
- **Pendiente:** el clon preexistente de `AccountStore.cs` sigue y no es de este cambio.
