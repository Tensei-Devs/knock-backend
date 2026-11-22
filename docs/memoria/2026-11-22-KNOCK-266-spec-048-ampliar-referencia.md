# Ampliar la búsqueda y referencia de mercado (spec 048, T-00, T-02, T-03, T-05, T-06 y T-08; spec 042 conciliada)

## Qué cambió
- `POST /api/v1/solicitudes/{id}/ampliar` (SOL-05): marca la oleada siguiente como pedida (`ampliadaHacia`) y programa `RepartirOleada` con `programadaPara = ahora`, en una transacción. Responde 200, 404 (ajena o inexistente), 409 `request_not_extendable` (no abierta, vencida, sin primera oleada repartida, 3 oleadas ya repartidas o la siguiente ya pedida) y 422 si el id no es un identificador válido. Política `solicitudes-ampliar` (10 por hora) y tope de cuerpo de 1 KB.
- `GET /api/v1/cotizaciones/referencia-mercado` (COT-03): solo técnicos (403 si no). Con 10 o más cotizaciones aceptadas en 90 días del mismo oficio y tipo de precio responde `origen: mercado` con p25, mediana y p75; si no, con 10 o más del oficio completo `origen: categoria`; si tampoco, `origen: sin_datos` con montos vacíos. Caché de 1 hora por réplica que se invalida al elegir una cotización. Política `cotizaciones-referencia` (30 por minuto).
- Dos rutas nuevas en el gateway, pruebas de integración `Spec048AmpliarTests` y `Spec048ReferenciaMercadoTests` y unitarias de los percentiles.
- Conciliación: las 8 tareas de la spec 042 ya estaban hechas y con CA-01 a CA-19 trazados; el único hueco de 048 que sigue abierto es CA-21 (evento `CotizacionElegida` a Mensajería) y la documentación de T-07.

## Por qué
- La marca `ampliadaHacia` y el filtro por `oleadaActual` hacen que dos peticiones iguales programen la oleada una sola vez, sin `Idempotency-Key` (R-18).
- La spec no define «tipo de trabajo» en las cotizaciones; se tomó el tipo de precio (`fijo`, `rango`, `visita`) porque mezclar una visita de 250 pesos con un arreglo completo no da una referencia útil. Una cotización por rango aporta el punto medio.
- Con menos de 10 muestras no hay rango, ni siquiera por categoría: una sola cifra revelaría un precio individual (D-06).
- Una solicitud ajena responde 404 y no 403 para no revelar que existe (§9).

## A evitar
- No cambiar el limite de 16 caracteres del oficio: `ICatalogApi.FindActiveTradeAsync` lanza `ArgumentException` con claves más largas, y eso daría 500 en lugar de 422.
- La caché es por réplica (igual que las demás de Mercado): con varias réplicas la referencia puede tardar hasta una hora en reflejar una elección hecha en otra.
- Pendiente fuera de esta unidad: `CotizacionElegida` en la bandeja y su consumidor en Mensajería (CA-21), `docs/endpoints.md` y `docs/rate-limit-matrix.md` (F3).

## Decidido
- `origen: sin_datos` (200 con montos nulos) cuando no hay 10 muestras: decisión técnica de esta unidad, a confirmar con José.
