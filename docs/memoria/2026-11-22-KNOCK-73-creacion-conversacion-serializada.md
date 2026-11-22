# KNOCK-73: creación de conversaciones simultánea sin 500 (CA-030-15, causa raíz)

## Qué cambió

- `ConversacionesStore.InsertAsync` ahora turna, dentro de la instancia, las creaciones de un mismo par (solicitud, técnico) con un candado por franja del par y, ya dentro del turno, relee el par: si existe responde «ya existía» (200 con la misma conversación) sin escribir; si no, inserta.
- Ante llave duplicada responde «ya existía»; ante conflicto de escritura (112) o interbloqueo de documentdb-local (24, «deadlock») reintenta con espera creciente antes de relanzar.

## Por qué

- Ocho creaciones simultáneas del mismo par insertan a la vez sobre el índice único `solicitudId_1_tecnicoId_1`. En documentdb-local se bloquean entre sí («Could not acquire lock for operation due to deadlock», código 24); el código solo trataba la llave duplicada, así que el interbloqueo llegaba como 500 y la prueba veía 0 respuestas 200 donde esperaba 7. Es el mismo defecto de KNOCK-295 (CA-046-69).

## A evitar

- No subir reintentos ni esperas para tapar el interbloqueo, ni cambiar la prueba: el turno por par más la relectura evita que se pisen.
- El turno es por instancia; entre varias instancias siguen valiendo el índice único, la relectura y el reintento.
