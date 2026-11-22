# Asistente de IA: turno, resumen, borrador, rango de precio y bitácora (spec 052)

## Qué cambió
- Controlador `AsistenteController` con ASI-01 a ASI-06, validadores, `AsistenteService`, `RangoPrecioService`, `BitacoraService` y `BitacoraStore` (`bitacoras`, índice único y TTL parcial por `venceEn`).
- Puertos `IAsistenteCuentas`, `IConversacionesAsistente`, `ISolicitudesAsistente` e `IReferenciaMercado` con respaldos que fallan cerrado hasta que existan las specs 030 y 048.
- Instrucciones de seguridad por gas puestas por el servidor; la salida del modelo pasa por el filtro de datos personales.
- `db/052-asistente-de-ia.js`, políticas `asistente-ia` y `asistente-consulta`, filas de docs.

## Por qué
La spec 052 pide sugerencias editables sin enviar nada sin confirmación y sin datos personales hacia el modelo ni en la respuesta.

## A evitar
- Aceptar identificadores de persona en el cuerpo. - Guardar un borrador como cotización. - Registrar mensajes completos.

## Decidido
- Un solo contador de consumo (tokens de la spec 024) en lugar de `uso_ia.unidades`.
- `tecnicoId` guarda el `sub`.

## PARA VALIDACIÓN DE JOSÉ
- Códigos `forbidden` y `price_reference_unavailable`; OpenAPI sin regenerar (archivo protegido). Rama apilada sobre `feature/KNOCK-63-cliente-ia-azure-openai`.