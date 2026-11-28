# KNOCK-295: auditoría de concurrencia, turno por clave en guardado de confianza y bloqueos

## Qué cambió

- Nuevo `KeyedGate` en `Knock.Hosting` (64 franjas por hash de la clave): un solo trabajo a la vez por clave dentro de la instancia. `TechnicianProfileStore` lo usa en lugar de su arreglo de candados propio.
- `TrustStore.SaveAsync` turna los guardados del mismo par (persona, técnico) y `BloqueosStore.AddAsync` los bloqueos del mismo par (quien bloquea, bloqueado); este último relee antes de insertar y responde «ya existía» sin escribir.
- Las pruebas CA-042-19 y CA-043-37 ya no repiten la ráfaga cuando el emulador contesta 500 por interbloqueo (se quitó `WithoutEmulatorDeadlockAsync`): ahora cualquier 500 hace fallar la prueba.

## Por qué

- Una ráfaga local de 42 rondas con un manejador de excepciones en el host mostró 500 por «Could not acquire lock for operation due to deadlock» (código 24) en `PUT /me/tecnicos-confianza/{id}` (6 rondas) y `POST /usuarios/{id}/bloqueo` (2 rondas); las pruebas pasaban solo porque repetían la ráfaga. Es el mismo defecto de CA-046-69 y CA-030-15: varias escrituras a la vez sobre un índice único se bloquean entre sí y los reintentos se agotan.
- El resto de las pruebas de concurrencia (cotizaciones, elecciones, llegadas, cancelaciones, reparto, dispositivos, semillas, reservas, almacenes del panel) no produjo ninguna excepción en las mismas 42 rondas.

## A evitar

- No tapar un 500 repitiendo la ráfaga en la prueba ni subir reintentos o esperas.
- El turno es por instancia; entre varias instancias siguen valiendo el índice único, la relectura y el reintento acotado de cada almacén.
