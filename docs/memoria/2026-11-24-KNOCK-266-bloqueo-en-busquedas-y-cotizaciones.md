# Spec 043 R-16 y R-18: el bloqueo también corta las búsquedas y la lista de cotizaciones

## Qué cambió
- `IRequestsApi.BlockedWithAsync` expone, hacia los demás módulos, la consulta de la proyección `bloqueos` de Mercado que ya usaban la cotización y el reparto (una sola consulta, ambos sentidos). No se duplicó ningún helper.
- `TechnicianBlockFilter` (módulo Tecnicos) cruza los perfiles de una página con el `usuarioId` de su cuenta y esa consulta. `GET /api/v1/tecnicos` y `GET /api/v1/explorar` (mejor calificados y cerca de ti) lo aplican solo con sesión y después de la caché compartida; las claves de caché no cambian.
- `QuoteComparisonService` (`GET /api/v1/solicitudes/{id}/cotizaciones`) quita las cotizaciones del técnico con un bloqueo en cualquier sentido con el cliente; el conteo y la regla de «puede elegir» se calculan con lo que queda. Nada se borra.
- Pruebas de integración con MongoDB real: `Spec043BloqueoBusquedasTests` (CA_043_26 y CA_043_28).

## Por qué
- R-16 pide que ninguna de las dos personas se vea en búsquedas y R-18 que las cotizaciones de la persona bloqueada no se muestren a quien bloqueó. Hasta ahora solo se cortaba al cotizar, al escribir y al repartir.

## A evitar
- No filtrar dentro de la caché compartida de la lista ni de Explorar: la clave no puede llevar datos de otra persona (spec 046, R-07).
- El mapa anónimo, el autocompletado de negocios y las fotos de «trabajos reales» no se filtran por persona: no llevan técnico identificable ni sesión. Cambiarlo cambiaría su contrato público.
- Una página filtrada puede traer menos elementos que `limite`; el cursor sigue siendo el del último elemento sin filtrar.
