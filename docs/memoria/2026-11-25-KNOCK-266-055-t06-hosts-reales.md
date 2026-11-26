# 055 T-06: pruebas que arrancan los cuatro proyectos (CA-04 y CA-05)

## Qué cambió
- `Spec055ForwardedHeadersTests` y `Spec055RedaccionTests` en `IntegrationTest/Hosting`, con `Spec055Hosts` (reutiliza `ServiceContractHosts` y añade la fábrica del gateway).

## Por qué
- CA-04 y CA-05 no tenían prueba. Las de 006 y 035 llaman a la biblioteca con una etiqueta por proyecto, así que no detectan que un servicio deje de conectarla; estas sí (comprobado quitando la conexión en Mensajería).

## A evitar
- Dar por probado «cada proyecto» sin arrancar el proyecto.
